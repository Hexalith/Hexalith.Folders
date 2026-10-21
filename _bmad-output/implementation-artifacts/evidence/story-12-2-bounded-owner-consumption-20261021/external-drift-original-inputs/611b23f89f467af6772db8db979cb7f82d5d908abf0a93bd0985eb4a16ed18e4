using System.Globalization;
using System.Collections;
using System.Reflection;
using System.Resources;
using System.Security.Claims;
using System.Text.RegularExpressions;

using AngleSharp.Dom;

using Bunit;

using Fluxor;

using Hexalith.FrontComposer.Contracts.Communication;
using Hexalith.FrontComposer.Contracts.Rendering;
using Hexalith.FrontComposer.Shell.Components.Layout;
using Hexalith.FrontComposer.Shell.State.Navigation;

using Hexalith.Tenants.Contracts.Commands;
using Hexalith.Tenants.Contracts.Enums;
using Hexalith.Tenants.Contracts.Queries;
using Hexalith.Tenants.UI.Components.Pages;
using Hexalith.Tenants.UI.Components.Tenants;
using Hexalith.Tenants.UI.Components.Tenants.Configuration;
using Hexalith.Tenants.UI.Components.Tenants.Lifecycle;
using Hexalith.Tenants.UI.Components.Tenants.Members;
using Hexalith.Tenants.UI.Components.Tenants.Metadata;
using Hexalith.Tenants.UI.Resources;
using Hexalith.Tenants.UI.Services;
using Hexalith.Tenants.UI.Services.Configuration;
using Hexalith.Tenants.UI.Services.Gateways;
using Hexalith.Tenants.UI.State;
using Hexalith.Tenants.UI.State.GlobalAdministrators;
using Hexalith.Tenants.UI.State.TenantAudit;
using Hexalith.Tenants.UI.State.TenantCommands;
using Hexalith.Tenants.UI.State.TenantDetail;
using Hexalith.Tenants.UI.State.TenantList;
using Hexalith.Tenants.UI.State.TenantUsers;
using Hexalith.EventStore.Client.Projections;
using Hexalith.EventStore.Contracts.Commands;
using Hexalith.EventStore.Contracts.Queries;

using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Localization;
using Microsoft.FluentUI.AspNetCore.Components;

using NSubstitute;

using Shouldly;

namespace Hexalith.Tenants.UI.Tests.Components;

public sealed partial class TenantDetailSurfaceTests : BunitContext
{
    // Protected search paging is a required scoped circuit service; the workspace fails loudly without it.
    public TenantDetailSurfaceTests()
    {
        Services.AddLocalization();
#if HFC_ROUTE_OPTIONS
        Services.AddScoped<Hexalith.FrontComposer.Shell.Services.NavigationFailureNotifier>();
#endif
        Services.AddScoped<TenantSearchPagingState>();
        Services.AddScoped<TenantAggregateCommandAdmissionGate>();
        Services.AddScoped<TenantCreateAttemptTracker>();
        Services.AddScoped(provider => provider.GetRequiredService<TenantCreateAttemptTracker>().Attempts);
        var viewport = new TenantHighImpactViewportObservation();
        viewport.Observe(ViewportTier.Desktop);
        Services.AddSingleton(viewport);
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider());
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("changed")]
    [InlineData("signed-out")]
    [InlineData("missing-subject")]
    [InlineData("faulted")]
    public void Authentication_transition_clears_all_retained_detail_evidence(string transition)
    {
        var authentication = new MutableAuthenticationStateProvider();
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" },
                TenantStatus.Active, [new TenantMember("private-member", TenantRole.TenantReader)]),
            ProjectionLifecycleState.Current, "tenant-sequence:41")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();
        gateway.GetGlobalAdministratorsAsync(Arg.Any<GlobalAdministratorsRequest>(), Arg.Any<GlobalAdministratorsSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(GlobalAdministratorsSnapshot.Ready(
                [new GlobalAdministratorRow("private-administrator", ReadModelFreshnessState.Current, ProjectionLifecycleState.Current)],
                null, false, "administrator-etag", ReadModelFreshnessState.Current) with
            {
                CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                Lifecycle = ProjectionLifecycleState.Current,
                ProjectionVersion = "global-v1",
                IsCompleteEvidence = true,
            });
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-row']");
        cut.WaitForAssertion(() => ((GlobalAdministratorsSnapshot)DetailField(cut, "_globalAdministratorsSnapshot")!)
            .Rows.ShouldHaveSingleItem().UserId.ShouldBe("private-administrator"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull());

        switch (transition)
        {
            case "pending": _ = authentication.NotifyPending(); break;
            case "changed": authentication.Notify(AdministratorPrincipal("operator.beta")); break;
            case "signed-out": authentication.Notify(new ClaimsPrincipal()); break;
            case "missing-subject": authentication.Notify(new ClaimsPrincipal(new ClaimsIdentity([], "test"))); break;
            default:
                authentication.NotifyPending().SetException(new InvalidOperationException("Identity unavailable"));
                break;
        }

        cut.WaitForAssertion(() =>
        {
            TenantDetailSnapshot detail = (TenantDetailSnapshot)DetailField(cut, "_snapshot")!;
            TenantUsersSnapshot members = (TenantUsersSnapshot)DetailField(cut, "_tenantUsersSnapshot")!;
            detail.Detail.ShouldBeNull();
            detail.ETag.ShouldBeNull();
            detail.ProjectionVersion.ShouldBeNull();
            detail.Configuration.IsAvailable.ShouldBeFalse();
            members.Rows.ShouldBeEmpty();
            members.ETag.ShouldBeNull();
            members.ProjectionVersion.ShouldBeNull();
            GlobalAdministratorsSnapshot? administrators = DetailField(cut, "_globalAdministratorsSnapshot") as GlobalAdministratorsSnapshot;
            if (administrators is not null)
            {
                administrators.Rows.ShouldBeEmpty();
                administrators.ETag.ShouldBeNull();
                administrators.ProjectionVersion.ShouldBeNull();
                administrators.IsCompleteEvidence.ShouldBeFalse();
            }
            DetailField(cut, "_auditProofCapabilityAvailable").ShouldBe(false);
            DetailField(cut, "_memberCursor").ShouldBeNull();
            ((ICollection)DetailField(cut, "_memberCursorHistory")!).Count.ShouldBe(0);
            cut.Markup.ShouldNotContain("private-member");
            cut.Markup.ShouldNotContain("billing.mode");
        });
    }

    [Theory]
    [InlineData("anonymous")]
    [InlineData("missing-subject")]
    [InlineData("subject-on-unauthenticated-identity")]
    [InlineData("conflicting-subjects")]
    [InlineData("multiple-authenticated-identities")]
    [InlineData("padded-subject")]
    public void Indeterminate_initial_identity_cannot_acquire_detail_data_or_audit_capability(string identityKind)
    {
        var authentication = new MutableAuthenticationStateProvider();
        ClaimsPrincipal principal = identityKind switch
        {
            "anonymous" => new ClaimsPrincipal(),
            "missing-subject" => new ClaimsPrincipal(new ClaimsIdentity([], "test")),
            "subject-on-unauthenticated-identity" => new ClaimsPrincipal([
                new ClaimsIdentity([], "test"), new ClaimsIdentity([new Claim("sub", "operator.alpha")])]),
            "conflicting-subjects" => new ClaimsPrincipal(new ClaimsIdentity([
                new Claim("sub", "operator.alpha"), new Claim("sub", "operator.beta")], "test")),
            "multiple-authenticated-identities" => new ClaimsPrincipal([
                new ClaimsIdentity([new Claim("sub", "operator.alpha")], "test"),
                new ClaimsIdentity([new Claim("sub", "operator.alpha")], "other")]),
            _ => AdministratorPrincipal(" operator.alpha "),
        };
        authentication.Notify(principal);
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"), projectionVersion: "tenant-sequence:1")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters.Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForElement("[data-testid='tenants-detail-unauthorized']");
        _ = gateway.DidNotReceiveWithAnyArgs().GetTenantAsync(default!, default, default);
        _ = gateway.DidNotReceiveWithAnyArgs().GetTenantUsersAsync(default!, default, default);
        _ = gateway.DidNotReceiveWithAnyArgs().GetTenantAuditAsync(default!, default, default);
        DetailField(cut, "_auditProofCapabilityAvailable").ShouldBe(false);
    }

    [Fact]
    public void Duplicate_identical_authenticated_subjects_preserve_same_caller_detail_capability()
    {
        var authentication = new MutableAuthenticationStateProvider();
        ClaimsPrincipal principal = AdministratorPrincipal();
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim("sub", "operator.alpha"));
        authentication.Notify(principal);
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"), projectionVersion: "tenant-sequence:1")));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters.Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull());
    }

    [Theory]
    [InlineData(null)]
    [InlineData("previous-caller")]
    public void Detail_audit_capability_rejects_unbound_or_previous_caller_empty_evidence(string? producer)
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"), projectionVersion: "tenant-sequence:1")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();
        gateway.GetTenantAuditAsync(Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => TenantAuditSnapshot.Empty(true, ReadModelFreshnessState.Current, "old-validator", call.Arg<TenantAuditRequest>()) with
            {
                CallerScope = producer is null ? null : TenantQueryGateway.AuditCallerScope(producer),
                Lifecycle = ProjectionLifecycleState.Current,
                ProjectionVersion = "old-version",
            });

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters.Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForAssertion(() => DetailField(cut, "_auditCapabilityResolved").ShouldBe(true));
        DetailField(cut, "_auditProofCapabilityAvailable").ShouldBe(false);
        cut.Find("[data-testid='tenants-detail-audit-entrypoint']").ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldBeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("previous-caller")]
    public void Member_empty_authorization_evidence_requires_current_caller_provenance(string? producer)
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"), projectionVersion: "tenant-sequence:1")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(TenantUsersSnapshot.Empty("tenant.alpha", true, null, "tenant-sequence:1", ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current) with { CallerScope = producer is null ? null : TenantQueryGateway.AuditCallerScope(producer) });

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters.Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        TenantUsersSnapshot members = (TenantUsersSnapshot)DetailField(cut, "_tenantUsersSnapshot")!;
        members.Kind.ShouldBe(TenantUsersSurfaceKind.Unavailable);
        members.IsAuthorizationScopedEmpty.ShouldBeFalse();
        members.ProjectionVersion.ShouldBeNull();
    }

    [Theory]
    [InlineData("metadata", "previous-caller")]
    [InlineData("metadata", null)]
    [InlineData("lifecycle", "previous-caller")]
    [InlineData("lifecycle", null)]
    [InlineData("audit", "previous-caller")]
    [InlineData("audit", null)]
    [InlineData("member-add", "previous-caller")]
    [InlineData("member-role", "previous-caller")]
    [InlineData("member-remove", "previous-caller")]
    public async Task Detail_proof_boundaries_reject_evidence_without_current_caller_provenance(string proofKind, string? producer)
    {
        ArgumentNullException.ThrowIfNull(proofKind);
        TenantDetail detail = Detail("tenant.alpha");
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(detail, projectionVersion: "tenant-sequence:41")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();
        TenantDetailSnapshot oldDetail = producer is null
            ? TenantDetailSnapshot.Ready(detail, "old-validator", ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current, "tenant-sequence:42")
            : ReadyWithSafeConfiguration(detail, projectionVersion: "tenant-sequence:42")
                .WithCallerScope(TenantQueryGateway.AuditCallerScope(producer));
        gateway.GetUpdateMetadataProjectionProofAsync(Arg.Any<UpdateTenant>(), Arg.Any<CancellationToken>()).Returns(oldDetail);
        gateway.GetLifecycleProjectionProofAsync(Arg.Any<TenantLifecycleCommandRequest>(), Arg.Any<CancellationToken>()).Returns(oldDetail);
        DateTimeOffset started = DateTimeOffset.UtcNow.AddMinutes(-1);
        ConfigureMetadataAuditProof(gateway, request => MetadataAuditSnapshot(request,
            MetadataAuditRow("message-a", "tenant.alpha", started.AddSeconds(1))) with
        {
            CallerScope = producer is null ? null : TenantQueryGateway.AuditCallerScope(producer),
        });
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        if (proofKind.StartsWith("member-", StringComparison.Ordinal))
        {
            gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>()).Returns(oldDetail);
        }
        (string method, object[] arguments) = proofKind switch
        {
            "metadata" => ("GetUpdateMetadataProjectionEvidenceAsync", new object[] {new UpdateTenant("tenant.alpha", "Alpha", null)}),
            "lifecycle" => ("GetLifecycleProjectionEvidenceAsync", new object[] {new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant), CancellationToken.None}),
            "audit" => ("GetUpdateMetadataAuditEvidenceAsync", new object[] {new UpdateTenant("tenant.alpha", "Alpha", null), "message-a", started}),
            "member-add" => ("GetAddMemberProjectionEvidenceAsync", new object[] {new AddUserToTenant("tenant.alpha", "member-a", TenantRole.TenantReader)}),
            "member-role" => ("GetChangeRoleProjectionEvidenceAsync", new object[] {new ChangeUserRole("tenant.alpha", "member-a", TenantRole.TenantReader)}),
            _ => ("GetRemoveMemberProjectionEvidenceAsync", new object[] {new RemoveUserFromTenant("tenant.alpha", "member-a")}),
        };

        object? proof = await InvokeDetailProofAsync(cut, method, arguments);

        proof.ShouldBeNull();
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("lifecycle")]
    [InlineData("audit")]
    [InlineData("member-add")]
    [InlineData("member-role")]
    [InlineData("member-remove")]
    public async Task Authentication_transition_cancels_and_rejects_ignored_cancellation_proof_completions(string proofKind)
    {
        var authentication = new MutableAuthenticationStateProvider();
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"), projectionVersion: "tenant-sequence:41")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();
        var pendingDetail = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var pendingAudit = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observed = default;
        TenantAuditRequest? auditRequest = null;
        gateway.GetUpdateMetadataProjectionProofAsync(Arg.Any<UpdateTenant>(), Arg.Any<CancellationToken>())
            .Returns(call => { observed = call.Arg<CancellationToken>(); return pendingDetail.Task; });
        gateway.GetLifecycleProjectionProofAsync(Arg.Any<TenantLifecycleCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(call => { observed = call.Arg<CancellationToken>(); return pendingDetail.Task; });
        gateway.GetTenantAuditAsync(Arg.Is<TenantAuditRequest>(request => request.From.HasValue), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => { observed = call.Arg<CancellationToken>(); auditRequest = call.Arg<TenantAuditRequest>(); return pendingAudit.Task; });
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        DateTimeOffset started = DateTimeOffset.UtcNow.AddMinutes(-1);
        (string method, object[] arguments) = proofKind switch
        {
            "metadata" => ("GetUpdateMetadataProjectionEvidenceAsync", new object[] {new UpdateTenant("tenant.alpha", "Alpha", null)}),
            "lifecycle" => ("GetLifecycleProjectionEvidenceAsync", new object[] {new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant), CancellationToken.None}),
            "audit" => ("GetUpdateMetadataAuditEvidenceAsync", new object[] {new UpdateTenant("tenant.alpha", "Alpha", null), "message-a", started}),
            "member-add" => ("GetAddMemberProjectionEvidenceAsync", new object[] {new AddUserToTenant("tenant.alpha", "member-a", TenantRole.TenantReader)}),
            "member-role" => ("GetChangeRoleProjectionEvidenceAsync", new object[] {new ChangeUserRole("tenant.alpha", "member-a", TenantRole.TenantReader)}),
            _ => ("GetRemoveMemberProjectionEvidenceAsync", new object[] {new RemoveUserFromTenant("tenant.alpha", "member-a")}),
        };
        if (proofKind.StartsWith("member-", StringComparison.Ordinal))
        {
            gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
                .Returns(call => { observed = call.Arg<CancellationToken>(); return pendingDetail.Task; });
        }
        Task<object?> proofTask = InvokeDetailProofAsync(cut, method, arguments);
        cut.WaitForAssertion(() => observed.CanBeCanceled.ShouldBeTrue());

        TaskCompletionSource<AuthenticationState> identity = authentication.NotifyPending();
        cut.WaitForAssertion(() => observed.IsCancellationRequested.ShouldBeTrue());
        pendingDetail.SetResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"), projectionVersion: "tenant-sequence:42"));
        if (auditRequest is not null)
        {
            pendingAudit.SetResult(MetadataAuditSnapshot(auditRequest, MetadataAuditRow("message-a", "tenant.alpha", started.AddSeconds(1))));
        }
        object? proof = await proofTask;

        proof.ShouldBeNull();
        cut.FindAll("[data-testid='tenants-detail-audit-entrypoint']").ShouldBeEmpty();
        identity.SetResult(new AuthenticationState(AdministratorPrincipal()));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        _ = gateway.Received(proofKind.StartsWith("member-", StringComparison.Ordinal) ? 3 : 2).GetTenantAsync(Arg.Is<TenantDetailRequest>(request => request.ETag == null),
            Arg.Is<TenantDetailSnapshot?>(previous => previous == null || previous.Detail == null && previous.ETag == null), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Lifecycle_proof_rejects_ignored_explicit_caller_cancellation()
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"), projectionVersion: "tenant-sequence:41")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();
        var pending = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.GetLifecycleProjectionProofAsync(Arg.Any<TenantLifecycleCommandRequest>(), Arg.Any<CancellationToken>()).Returns(pending.Task);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        using var cancellation = new CancellationTokenSource();
        Task<object?> read = InvokeDetailProofAsync(cut, "GetLifecycleProjectionEvidenceAsync",
            [new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant), cancellation.Token]);

        cancellation.Cancel();
        pending.SetResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"), projectionVersion: "tenant-sequence:42"));

        (await read).ShouldBeNull();
    }

    [Fact]
    public async Task Lifecycle_reauthorization_rejects_same_subject_authentication_transition_while_suspended()
    {
        var authentication = new MutableAuthenticationStateProvider();
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"), projectionVersion: "tenant-sequence:41")), composition);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        composition.ResolutionGate = pending;
        int priorResolutions = composition.AsyncResolutionCount;
        Task<object?> read = InvokeDetailProofAsync(cut, "ReauthorizeLifecycleAsync", [CancellationToken.None]);
        cut.WaitForAssertion(() => composition.AsyncResolutionCount.ShouldBe(priorResolutions + 1));
        composition.ResolutionGate.ShouldBeNull();

        authentication.Notify(AdministratorPrincipal());
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        pending.SetResult();

        (await read).ShouldBe(TenantLifecycleAuthorizationReflectionState.Indeterminate);
    }

    private static object? DetailField(IRenderedComponent<TenantDetailPage> cut, string name)
        => typeof(TenantDetailPage).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cut.Instance);

    private static Task<object?> InvokeDetailProofAsync(IRenderedComponent<TenantDetailPage> cut, string method, object[] arguments)
        => cut.InvokeAsync(async () =>
        {
            Task task = (Task)typeof(TenantDetailPage).GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cut.Instance, arguments)!;
            await task;
            return task.GetType().GetProperty("Result")!.GetValue(task);
        });

    [Fact]
    public void Removing_detail_audit_focus_clears_notices_and_allows_same_focus_again()
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail(
            "tenant.alpha", new Dictionary<string, string>(), TenantStatus.Active, []))));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler<string> focus = module.Setup<string>("restoreAuditFocus", _ => true);
        focus.SetResult("missing");
        NavigationManager navigation = Services.GetRequiredService<NavigationManager>();
        navigation.NavigateTo("/tenants/tenant.alpha?auditFocus=tenants-detail-identity&auditPartialReturn=true");
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-audit-return-notice']");
        cut.Find("[data-testid='tenants-audit-partial-return']");

        navigation.NavigateTo("/tenants/tenant.alpha");
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-audit-return-notice']").ShouldBeEmpty());
        cut.FindAll("[data-testid='tenants-audit-partial-return']").ShouldBeEmpty();

        navigation.NavigateTo("/tenants/tenant.alpha?auditFocus=tenants-detail-identity");
        cut.WaitForAssertion(() => focus.Invocations.Count.ShouldBe(2));
    }

    [Fact]
    public void Detail_audit_focus_timeout_shows_missing_origin_notice()
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail(
            "tenant.alpha", new Dictionary<string, string>(), TenantStatus.Active, []))));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        module.Setup<string>("restoreAuditFocus", _ => true)
            .SetException(new TaskCanceledException("Focus helper timed out."));
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha?auditFocus=tenants-detail-identity");

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForElement("[data-testid='tenants-audit-return-notice']");
        cut.Find("#tenants-detail-identity").GetAttribute("tabindex").ShouldBe("-1");
    }

    [Fact]
    public void Detail_page_loads_through_gateway_and_renders_operational_overview()
    {
        const string tenantId = "tenant.alpha";
        const string memberId = "  user/%2F?x=é&glyph=о  ";
        TenantDetail detail = Detail(
            tenantId,
            new Dictionary<string, string> { ["billing.mode"] = "trial" },
            TenantStatus.Active,
            [
                new TenantMember(memberId, TenantRole.TenantOwner),
                new TenantMember("reader-user", TenantRole.TenantReader),
            ]);
        List<TenantDetailRequest> requests = [];
        RegisterServices(call =>
        {
            requests.Add(call.ArgAt<TenantDetailRequest>(0));
            return Task.FromResult(ReadyWithSafeConfiguration(detail));
        });
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsClipboard.js");
        JSRuntimeInvocationHandler tenantWrite = module.SetupVoid("writeText", tenantId).SetVoidResult();
        JSRuntimeInvocationHandler memberWrite = module.SetupVoid("writeText", memberId).SetVoidResult();

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha?returnUrl=%2Ftenants%3Fsearch%3Dalpha%26selected%3Dtenant.alpha");

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, tenantId));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");

        requests.ShouldHaveSingleItem().TenantId.ShouldBe(tenantId);
        cut.Find("[data-testid='tenants-detail-back']").GetAttribute("href").ShouldBe("/tenants?search=alpha&selected=tenant.alpha");
        cut.Find("[data-testid='tenants-detail-truth-state']").TextContent.ShouldContain("Current");
        cut.Find(".tenant-detail__literal").TextContent.ShouldBe(tenantId);
        cut.Find("[data-testid='tenants-member-user-id'] code").TextContent.ShouldBe(memberId);
        cut.Find("[data-testid='tenants-edit-metadata-flow']").TextContent.ShouldContain("Alpha");
        cut.Find("[data-testid='tenants-detail-copy-reference']").GetAttribute("data-copy-kind").ShouldBe("TenantId");
        cut.Find("[data-testid='tenants-detail-copy-reference']").TextContent.ShouldContain("Copy");
        cut.Find("[data-testid='tenants-lifecycle-actions']");
        cut.Find("[data-testid='tenants-lifecycle-current-status']").TextContent.ShouldContain("Active");
        // This fixture has neither reflected authority nor a projection version. Permission-safe precedence
        // must withhold the lower-level projection detail from the unauthorized/indeterminate caller.
        cut.Find("[data-testid='tenants-lifecycle-disable-unavailable-reason']").TextContent
            .ShouldContain("authority", Case.Insensitive);
        // Asserted past the shared prefix: both summary variants begin "{0} members visible on this page",
        // so the previous ShouldContain("2 members") passed on either branch. This fixture builds the detail
        // snapshot with the default Current lifecycle and no projection version, so the evidence is not
        // version-consistent and the owner-context variant is correctly withheld. Both branches are covered
        // by Member_governance_claims_require_current_lifecycle_and_a_stated_matching_projection_version.
        cut.Find("[data-testid='tenants-detail-member-summary']").TextContent
            .ShouldContain("2 members visible on this page. Owner context is unavailable");
        cut.Find("[data-testid='tenants-detail-configuration-summary']").TextContent.ShouldContain("1 visible configuration keys");
        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain("billing.mode");
        cut.Find(".tenant-detail__literal").GetAttribute("aria-label").ShouldBe($"Full tenant identifier {tenantId}");
        cut.Markup.ShouldContain("aria-label=\"Tenant status Active\"");

        cut.Find("[data-surface-testid='tenants-detail-copy-reference']").Click();
        cut.WaitForAssertion(() => tenantWrite.Invocations.Count.ShouldBe(1));
        tenantWrite.Invocations.Single().Arguments[0].ShouldBe(tenantId);

        cut.Find("[data-surface-testid='tenants-member-copy-reference']").Click();
        cut.WaitForAssertion(() => memberWrite.Invocations.Count.ShouldBe(1));
        memberWrite.Invocations.Single().Arguments[0].ShouldBe(memberId);
    }

    [Fact]
    public async Task Fluxor_viewport_dispatch_drives_the_scoped_detail_page_action_eligibility()
    {
        Services.RemoveAll<TenantHighImpactViewportObservation>();
        Services.AddScoped<TenantHighImpactViewportObservation>();
        Services.AddFluxor(options => options.ScanAssemblies(typeof(TenantHighImpactViewportEffects).Assembly));
        RegisterServices(
            _ => Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")),
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        IStore store = Services.GetRequiredService<IStore>();
        await store.InitializeAsync();
        IDispatcher dispatcher = Services.GetRequiredService<IDispatcher>();
        TenantHighImpactViewportObservation observation = Services.GetRequiredService<TenantHighImpactViewportObservation>();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-lifecycle-disable']")
            .GetAttribute("disabled").ShouldNotBeNull();

        dispatcher.Dispatch(new ViewportTierChangedAction(ViewportTier.Tablet));
        cut.WaitForAssertion(() =>
        {
            observation.State.ShouldBe(TenantHighImpactViewportState.Safe);
            cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldBeNull();
        });
        TenantHighImpactActionEvidence removeEntryEvidence = (TenantHighImpactActionEvidence)typeof(TenantDetailPage)
            .GetMethod("HighImpactEvidence", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cut.Instance, [TenantHighImpactAction.RemoveConfiguration])!;
        removeEntryEvidence.TargetState.ShouldBe(TenantHighImpactTargetState.Unknown);

        dispatcher.Dispatch(new ViewportTierChangedAction((ViewportTier)255));
        cut.WaitForAssertion(() =>
        {
            observation.State.ShouldBe(TenantHighImpactViewportState.Unknown);
            cut.Find("[data-testid='tenants-lifecycle-disable']").GetAttribute("disabled").ShouldNotBeNull();
        });
    }

    [Fact]
    public void Detail_page_requires_a_current_authoritative_audit_read_for_remove_actions()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "tenant-sequence:1")));
        gateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(MemberSnapshot(Detail("tenant.alpha")) with
            {
                ProjectionVersion = "tenant-sequence:1",
            }));
        gateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.ArgAt<TenantAuditRequest>(0);
                return TenantAuditSnapshot.Stale([], null, false, "audit-etag", request) with
                {
                    CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                    Lifecycle = ProjectionLifecycleState.Stale,
                    ProjectionVersion = "audit-v1",
                };
            });
        RegisterDetailPageServices(gateway, configureAuthoritativeAuditCapability: false);

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-member-summary']");

        IRenderedComponent<MemberAccessReview> access = cut.FindComponent<MemberAccessReview>();
        access.Instance.AuditProofCapabilityAvailable.ShouldBeFalse();
        gateway.Received().GetTenantAuditAsync(
            Arg.Is<TenantAuditRequest>(request => request.TenantId == "tenant.alpha" && request.PageSize == 1),
            Arg.Any<TenantAuditSnapshot?>(),
            Arg.Any<CancellationToken>());
        cut.FindAll("[data-testid='tenants-remove-member-open']").ShouldBeEmpty();
        cut.Markup.ShouldContain("missing audit proof", Case.Insensitive);
    }

    [Fact]
    public void Detail_page_loads_all_global_administrator_pages_before_resolving_platform_standing()
    {
        TenantDetail detail = Detail("tenant.alpha");
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                detail,
                ProjectionLifecycleState.Current,
                "tenant-sequence:1")));
        gateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(detail)));
        List<GlobalAdministratorsRequest> requests = [];
        gateway.GetGlobalAdministratorsAsync(
                Arg.Any<GlobalAdministratorsRequest>(),
                Arg.Any<GlobalAdministratorsSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                GlobalAdministratorsRequest request = call.ArgAt<GlobalAdministratorsRequest>(0);
                requests.Add(request);
                return request.Cursor is null
                    ? GlobalAdministratorsSnapshot.Ready(
                        [new GlobalAdministratorRow(
                            "another-admin",
                            ReadModelFreshnessState.Current,
                            ProjectionLifecycleState.Current)],
                        nextCursor: "opaque-ga-page-2",
                        hasMore: true,
                        eTag: "ga-etag-1",
                        ReadModelFreshnessState.Current) with
                    {
                        CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                        Lifecycle = ProjectionLifecycleState.Current,
                        ProjectionVersion = "ga-v1",
                        RequestCursor = request.Cursor,
                    }
                    : GlobalAdministratorsSnapshot.Ready(
                        [new GlobalAdministratorRow(
                            "reader-user",
                            ReadModelFreshnessState.Current,
                            ProjectionLifecycleState.Current)],
                        nextCursor: null,
                        hasMore: false,
                        eTag: "ga-etag-2",
                        ReadModelFreshnessState.Current) with
                    {
                        CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                        Lifecycle = ProjectionLifecycleState.Current,
                        ProjectionVersion = "ga-v1",
                        RequestCursor = request.Cursor,
                    };
            });
        RegisterDetailPageServices(gateway);

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() =>
        {
            GlobalAdministratorsSnapshot standing = cut.FindComponent<MemberAccessReview>()
                .Instance.GlobalAdministrators.ShouldNotBeNull();
            standing.IsCompleteEvidence.ShouldBeTrue();
            standing.Rows.ShouldContain(row => row.UserId == "reader-user");
        });

        requests.Select(static request => request.Cursor).ShouldBe([null, "opaque-ga-page-2"]);
        int readerIndex = detail.Members
            .Select((member, index) => (member.UserId, index))
            .First(pair => pair.UserId == "reader-user")
            .index;
        cut.FindAll("[data-testid='tenants-remove-member-open']")[readerIndex].Click();
        cut.Find("[data-testid='tenants-remove-member-global-admin-risk']").TextContent
            .ShouldContain("will not remove global-administrator authority");
    }

    [Fact]
    public void Supplementary_global_administrator_read_does_not_block_primary_detail_rendering()
    {
        TenantDetail detail = Detail("tenant.alpha");
        var pendingStanding = new TaskCompletionSource<GlobalAdministratorsSnapshot>(
            TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                detail,
                ProjectionLifecycleState.Current,
                "tenant-sequence:1")));
        gateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(detail)));
        gateway.GetGlobalAdministratorsAsync(
                Arg.Any<GlobalAdministratorsRequest>(),
                Arg.Any<GlobalAdministratorsSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(pendingStanding.Task);
        RegisterDetailPageServices(gateway);

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForElement("[data-testid='tenants-detail-member-summary']");
        pendingStanding.Task.IsCompleted.ShouldBeFalse();
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("reader-user");

        pendingStanding.SetResult(GlobalAdministratorsSnapshot.Empty(
            isAuthorizationScoped: true,
            ReadModelFreshnessState.Current,
            eTag: "ga-empty") with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "ga-v1",
        });
        cut.WaitForAssertion(() => cut.FindComponent<MemberAccessReview>()
            .Instance.GlobalAdministrators.ShouldNotBeNull());
    }

    [Theory]
    [InlineData("stale", 1)]
    [InlineData("version-mismatch", 2)]
    [InlineData("missing-cursor", 1)]
    [InlineData("cyclic-cursor", 2)]
    public void Incomplete_global_administrator_walk_keeps_absent_platform_standing_unresolved(
        string scenario,
        int expectedCalls)
    {
        TenantDetail detail = Detail("tenant.alpha");
        ITenantQueryGateway gateway = CreateCurrentDetailPageGateway();
        int calls = 0;
        gateway.GetGlobalAdministratorsAsync(
                Arg.Any<GlobalAdministratorsRequest>(),
                Arg.Any<GlobalAdministratorsSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                GlobalAdministratorsRequest request = call.ArgAt<GlobalAdministratorsRequest>(0);
                calls++;
                if (scenario == "stale")
                {
                    return GlobalAdministratorsSnapshot.Stale([], null, false, "ga-stale") with
                    {
                        CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                        Lifecycle = ProjectionLifecycleState.Stale,
                        ProjectionVersion = "ga-v1",
                    };
                }

                string? nextCursor = scenario switch
                {
                    "missing-cursor" => null,
                    "cyclic-cursor" => "cycle",
                    _ => calls == 1 ? "page-2" : null,
                };
                bool hasMore = scenario is "missing-cursor" or "cyclic-cursor" || calls == 1;
                return GlobalAdministratorsSnapshot.Ready(
                    [new GlobalAdministratorRow(
                        $"admin-{calls}",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current)],
                    nextCursor,
                    hasMore,
                    $"ga-etag-{calls}",
                    ReadModelFreshnessState.Current) with
                {
                    CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                    Lifecycle = ProjectionLifecycleState.Current,
                    ProjectionVersion = scenario == "version-mismatch" && calls > 1 ? "ga-v2" : "ga-v1",
                    RequestCursor = request.Cursor,
                };
            });
        RegisterDetailPageServices(gateway);

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, detail.TenantId));

        cut.WaitForAssertion(() =>
        {
            calls.ShouldBe(expectedCalls);
            GlobalAdministratorsSnapshot standing = cut.FindComponent<MemberAccessReview>()
                .Instance.GlobalAdministrators.ShouldNotBeNull();
            standing.IsCompleteEvidence.ShouldBeFalse();
        });
        cut.WaitForAssertion(() => cut.FindComponent<MemberAccessReview>()
            .Instance.AuditProofCapabilityAvailable.ShouldBeTrue());
        OpenReaderRemoval(cut, detail);
        cut.Find("[data-testid='tenants-remove-member-platform-standing']").TextContent
            .ShouldContain("unproven", Case.Insensitive);
    }

    [Fact]
    public void Global_administrator_walk_stops_at_page_cap_and_keeps_absence_unresolved()
    {
        TenantDetail detail = Detail("tenant.alpha");
        ITenantQueryGateway gateway = CreateCurrentDetailPageGateway();
        int calls = 0;
        gateway.GetGlobalAdministratorsAsync(
                Arg.Any<GlobalAdministratorsRequest>(),
                Arg.Any<GlobalAdministratorsSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                GlobalAdministratorsRequest request = call.ArgAt<GlobalAdministratorsRequest>(0);
                calls++;
                return GlobalAdministratorsSnapshot.Ready(
                    [new GlobalAdministratorRow(
                        $"ga-admin-{calls}",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current)],
                    nextCursor: $"ga-page-{calls}",
                    hasMore: true,
                    eTag: $"ga-etag-{calls}",
                    ReadModelFreshnessState.Current) with
                {
                    CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                    Lifecycle = ProjectionLifecycleState.Current,
                    ProjectionVersion = "ga-v1",
                    RequestCursor = request.Cursor,
                };
            });
        RegisterDetailPageServices(gateway);

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, detail.TenantId));

        cut.WaitForAssertion(() =>
        {
            calls.ShouldBe(CursorHistory.DefaultMaximum);
            cut.FindComponent<MemberAccessReview>().Instance.GlobalAdministrators
                .ShouldNotBeNull().IsCompleteEvidence.ShouldBeFalse();
        });
        OpenReaderRemoval(cut, detail);
        cut.Find("[data-testid='tenants-remove-member-platform-standing']").TextContent
            .ShouldContain("unproven", Case.Insensitive);
    }

    [Fact]
    public async Task Route_change_cancels_supplementary_reads_and_late_tenant_a_results_cannot_overwrite_tenant_b()
    {
        var lateAdministrators = new TaskCompletionSource<GlobalAdministratorsSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var lateAudit = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var tenantATokens = new List<CancellationToken>();
        ITenantQueryGateway gateway = CreateCurrentDetailPageGateway();
        int globalAdministratorCalls = 0;
        gateway.GetGlobalAdministratorsAsync(
                Arg.Any<GlobalAdministratorsRequest>(),
                Arg.Any<GlobalAdministratorsSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                CancellationToken token = call.ArgAt<CancellationToken>(2);
                if (Interlocked.Increment(ref globalAdministratorCalls) == 1)
                {
                    tenantATokens.Add(token);
                    return lateAdministrators.Task;
                }

                return Task.FromResult(
                    GlobalAdministratorsSnapshot.Empty(true, ReadModelFreshnessState.Current, "ga-b") with
                    {
                        CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                        Lifecycle = ProjectionLifecycleState.Current,
                        ProjectionVersion = "ga-b-v1",
                    });
            });
        gateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.ArgAt<TenantAuditRequest>(0);
                if (request.TenantId == "tenant.a")
                {
                    tenantATokens.Add(call.ArgAt<CancellationToken>(2));
                    return lateAudit.Task;
                }

                return Task.FromResult(
                    TenantAuditSnapshot.Stale([], null, false, "audit-b", request) with
                    {
                        CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                        Lifecycle = ProjectionLifecycleState.Stale,
                        ProjectionVersion = "audit-b-v1",
                    });
            });
        RegisterDetailPageServices(gateway, configureAuthoritativeAuditCapability: false);

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.a"));
        cut.WaitForElement("[data-testid='tenants-detail-member-summary']");
        cut.Render(parameters => parameters.Add(page => page.TenantId, "tenant.b"));
        cut.WaitForAssertion(() => cut.Find(".tenant-detail__literal").TextContent.ShouldBe("tenant.b"));
        await WaitUntilAsync(
            () => tenantATokens.Count >= 2 && tenantATokens.All(static token => token.IsCancellationRequested),
            TimeSpan.FromSeconds(2));

        lateAdministrators.SetResult(GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow("reader-user", ReadModelFreshnessState.Current, ProjectionLifecycleState.Current)],
            null,
            false,
            "ga-a",
            ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "ga-a-v1",
        });
        TenantAuditRequest tenantARequest = new("tenant.a", PageSize: 1);
        lateAudit.SetResult(TenantAuditSnapshot.Empty(true, ReadModelFreshnessState.Current, "audit-a", tenantARequest) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = "audit-a-v1",
        });
        await Task.Delay(50);

        MemberAccessReview access = cut.FindComponent<MemberAccessReview>().Instance;
        access.GlobalAdministrators.ShouldNotBeNull().Rows.ShouldBeEmpty();
        access.AuditProofCapabilityAvailable.ShouldBeFalse();
    }

    [Fact]
    public async Task Failed_global_administrator_refresh_retains_rows_only_as_incomplete_unknown_evidence()
    {
        ITenantQueryGateway gateway = CreateCurrentDetailPageGateway();
        int calls = 0;
        gateway.GetGlobalAdministratorsAsync(
                Arg.Any<GlobalAdministratorsRequest>(),
                Arg.Any<GlobalAdministratorsSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => ++calls == 1
                ? GlobalAdministratorsSnapshot.Ready(
                    [new GlobalAdministratorRow("reader-user", ReadModelFreshnessState.Current, ProjectionLifecycleState.Current)],
                    null,
                    false,
                    "ga-current",
                    ReadModelFreshnessState.Current) with
                {
                    CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                    Lifecycle = ProjectionLifecycleState.Current,
                    ProjectionVersion = "ga-v1",
                }
                : throw new InvalidOperationException("supplementary refresh fault"));
        RegisterDetailPageServices(gateway);

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.FindComponent<MemberAccessReview>().Instance.GlobalAdministrators
            .ShouldNotBeNull().IsCompleteEvidence.ShouldBeTrue());

        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance.OnProjectionRefreshRequested.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            GlobalAdministratorsSnapshot retained = cut.FindComponent<MemberAccessReview>()
                .Instance.GlobalAdministrators.ShouldNotBeNull();
            retained.Rows.ShouldContain(row => row.UserId == "reader-user");
            retained.IsCompleteEvidence.ShouldBeFalse();
            retained.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
            retained.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
            retained.ProjectionVersion.ShouldBeNull();
        });
    }

    [Fact]
    public void Detail_page_is_composed_from_the_frontcomposer_aggregate_detail_wrapper()
    {
        // cc-2026-06-21 extraction guard: the detail page reuses FcAggregateDetailPage<TItem> (the shared
        // FC-DTL chrome) and maps its ready snapshot onto the constrained-measure wrapper instead of a
        // Tenants-local detail page shell. Keeps the rebase from silently regressing.
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"))));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");

        FcAggregateDetailPage<TenantDetail> wrapper = cut.FindComponent<FcAggregateDetailPage<TenantDetail>>().Instance;
        wrapper.LayoutMode.ShouldBe(FcPageLayoutMode.Constrained);
        wrapper.State.ShouldBe(FcAggregateDetailState.Ready);
    }

    [Fact]
    public void Detail_lifecycle_actions_use_the_async_circuit_authorization_decision()
    {
        RegisterServices(
            _ => Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")),
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-lifecycle-disable']")
                .GetAttribute("aria-disabled")
                .ShouldBe("false"));
    }

    [Fact]
    public void Detail_page_wires_projection_version_reauthorization_and_authoritative_lifecycle_proof_end_to_end()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        TenantDetail active = Detail("tenant.alpha");
        TenantDetail disabled = Detail(
            "tenant.alpha",
            active.Configuration,
            TenantStatus.Disabled,
            active.Members);
        int proofReads = 0;
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                active,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(active) with
            {
                ProjectionVersion = "tenant-sequence:41",
            }));
        queryGateway.GetLifecycleProjectionProofAsync(
                Arg.Any<TenantLifecycleCommandRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantLifecycleCommandRequest request = call.ArgAt<TenantLifecycleCommandRequest>(0);
                request.TenantId.ShouldBe("tenant.alpha");
                int read = Interlocked.Increment(ref proofReads);
                return Task.FromResult(ReadyWithSafeConfiguration(
                    read == 1 ? active : disabled,
                    ProjectionLifecycleState.Current,
                    read == 1 ? "tenant-sequence:41" : "tenant-sequence:42"));
            });
        ConfigureAuthoritativeAuditCapability(queryGateway);
        var commandGateway = new TrackingLifecycleCommandGateway();
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddSingleton<TenantLifecycleAttemptTracker>();
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-lifecycle-disable']")
            .GetAttribute("disabled").ShouldBeNull());
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("[data-testid='tenants-lifecycle-command-flow'] form").Submit();

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        commandGateway.DisableCallCount.ShouldBe(1);
        commandGateway.LastDisableRequest.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
        commandGateway.LastMessageId.ShouldNotBeNullOrWhiteSpace();
        commandGateway.StatusCallCount.ShouldBeGreaterThanOrEqualTo(1);
        proofReads.ShouldBeGreaterThanOrEqualTo(2);
        composition.AsyncResolutionCount.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public async Task Matching_signalr_notification_forwards_an_authoritative_refresh_nudge_to_the_page_lifecycle_flow()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backendSubscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        TenantDetail active = Detail("tenant.alpha");
        TenantDetail disabled = Detail("tenant.alpha", active.Configuration, TenantStatus.Disabled, active.Members);
        int detailReads = 0;
        int proofReads = 0;
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                int read = Interlocked.Increment(ref detailReads);
                return Task.FromResult(ReadyWithSafeConfiguration(
                    read == 1 ? active : disabled,
                    ProjectionLifecycleState.Current,
                    read == 1 ? "tenant-sequence:41" : "tenant-sequence:42"));
            });
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(active) with { ProjectionVersion = "tenant-sequence:41" }));
        queryGateway.GetLifecycleProjectionProofAsync(
                Arg.Any<TenantLifecycleCommandRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                int read = Interlocked.Increment(ref proofReads);
                return Task.FromResult(ReadyWithSafeConfiguration(
                    read == 1 ? active : disabled,
                    ProjectionLifecycleState.Current,
                    read == 1 ? "tenant-sequence:41" : "tenant-sequence:42"));
            });
        ConfigureAuthoritativeAuditCapability(queryGateway);
        backendSubscription.SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var commandGateway = new TrackingLifecycleCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        Services.AddSingleton(queryGateway);
        Services.AddSingleton(backendSubscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<TenantLifecycleAttemptTracker>();
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-lifecycle-disable']")
            .GetAttribute("disabled").ShouldBeNull());
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("[data-testid='tenants-lifecycle-command-flow'] form").Submit();
        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted));
        await backendSubscription.Received(1)
            .SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>());

        commandGateway.Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 };
        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.alpha");

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        commandGateway.DisableCallCount.ShouldBe(1);
        commandGateway.StatusCallCount.ShouldBeGreaterThanOrEqualTo(2);
        detailReads.ShouldBeGreaterThanOrEqualTo(2);
        proofReads.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Fact]
    public void Page_remount_reacquires_a_retained_lifecycle_lease_owned_by_the_tracker_without_redispatch()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        TenantDetail detail = Detail("tenant.alpha");
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                detail,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(detail) with { ProjectionVersion = "tenant-sequence:41" }));
        queryGateway.GetLifecycleProjectionProofAsync(
                Arg.Any<TenantLifecycleCommandRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                detail,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        ConfigureAuthoritativeAuditCapability(queryGateway);
        var tracker = new TenantLifecycleAttemptTracker();
        var intent = new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant);
        TenantLifecycleCommandSnapshot retained = TenantLifecycleCommandSnapshot
            .Idle(detail)
            .Previewed(intent, detail, "tenant-sequence:41")
            .RequestSent(intent, detail, "tenant-sequence:41", "message-life")
            .Accepted(TenantCommandSubmissionResult.Accepted("message-life", "correlation-life"));
        tracker.Remember(retained).ShouldBeTrue();
        var gate = new TenantAggregateCommandAdmissionGate();
        string lockKey = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        gate.TryAcquire(lockKey, tracker.LeaseOwner).ShouldBeTrue();
        var commandGateway = new TrackingLifecycleCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        Services.RemoveAll<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton(gate);
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton(tracker);
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.MessageId.ShouldBe("message-life"));
        cut.FindComponent<TenantLifecycleCommandFlow>().Instance.Snapshot.State
            .ShouldBe(TenantCommandLifecycleState.Accepted);
        gate.IsOwnedBy(lockKey, tracker.LeaseOwner).ShouldBeTrue();
        commandGateway.DisableCallCount.ShouldBe(0);
        commandGateway.StatusCallCount.ShouldBe(1);
    }

    [Fact]
    public void Page_remount_releases_an_expired_tracker_owned_lifecycle_gate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        TenantDetail detail = Detail("tenant.alpha");
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                detail,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(detail) with { ProjectionVersion = "tenant-sequence:41" }));
        ConfigureAuthoritativeAuditCapability(queryGateway);
        DateTimeOffset now = DateTimeOffset.Parse("2026-06-01T12:00:00Z");
        var tracker = new TenantLifecycleAttemptTracker(() => now);
        tracker.BeginDispatch(
            new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant),
            "tenant-sequence:41",
            now);
        var gate = new TenantAggregateCommandAdmissionGate();
        string lockKey = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        gate.TryAcquire(lockKey, tracker.LeaseOwner).ShouldBeTrue();
        now += TenantLifecycleCommandSnapshot.MaximumRetainedAttemptDuration;
        Services.RemoveAll<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton(gate);
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new TrackingLifecycleCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton(tracker);
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForAssertion(() => cut.Find(".tenant-detail__literal").TextContent.ShouldBe("tenant.alpha"));
        tracker.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsOwnedBy(lockKey, tracker.LeaseOwner).ShouldBeFalse();
    }

    [Fact]
    public void Page_remount_adopts_retained_remove_attempt_without_redispatch()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        TenantDetail detail = Detail("tenant.alpha");
        queryGateway.SupportsRemoveConfigurationPreview.Returns(true);
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                detail,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(detail) with { ProjectionVersion = "tenant-sequence:41" }));
        TenantRemoveConfigurationIntent intent = new("tenant.alpha", "billing", "billing.mode");
        queryGateway.GetRemoveConfigurationProjectionProofAsync(intent, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantConfigurationProjectionProof.Create(
                intent.TenantId,
                TenantConfigurationProjectionProofKind.RemoveNotConfirmed,
                "tenant-sequence:41",
                intent.AttemptFingerprint)));
        ConfigureAuthoritativeAuditCapability(queryGateway);
        var tracker = new TenantRemoveConfigurationAttemptTracker();
        (string messageId, DateTimeOffset startedAt) = tracker.BeginDispatch(
            intent,
            "tenant-sequence:41",
            DateTimeOffset.UtcNow);
        TenantRemoveConfigurationPreview preview = TenantRemoveConfigurationPreview.Create(
            intent,
            TenantStatus.Active,
            TenantRemoveConfigurationCurrentState.Present,
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            "tenant-sequence:41",
            isAuthorized: true);
        TenantRemoveConfigurationCommandSnapshot retained = TenantRemoveConfigurationCommandSnapshot.Idle()
            .Previewed(preview)
            .RequestSent(preview, messageId, startedAt)
            .Accepted(TenantCommandSubmissionResult.Accepted(messageId, messageId));
        tracker.Remember(retained).ShouldBeTrue();
        var gate = new TenantAggregateCommandAdmissionGate();
        string lockKey = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        gate.TryAcquire(lockKey, tracker.LeaseOwner).ShouldBeTrue();
        var commandGateway = new TrackingRemoveConfigurationCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        Services.RemoveAll<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton(gate);
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton(tracker);
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForAssertion(() => cut.FindComponent<RemoveTenantConfigurationFlow>()
            .Instance.Snapshot.MessageId.ShouldBe(messageId));
        cut.Find("[data-testid='tenants-config-remove-flow']");
        gate.IsOwnedBy(lockKey, tracker.LeaseOwner).ShouldBeTrue();
        commandGateway.RemoveCallCount.ShouldBe(0);
        commandGateway.StatusCallCount.ShouldBeGreaterThanOrEqualTo(1);
        commandGateway.LastStatusHandle.ShouldNotBeNull().MessageId.ShouldBe(messageId);

        // The aggregate id is what makes GetStatusAsync reject a status response belonging to another
        // tenant: TenantCommandGateway treats a blank handle.AggregateId as vacuously verified, so dropping
        // it here would silently downgrade the destructive flow to message-id-only identity. Asserted the
        // same way as the set sibling above.
        commandGateway.LastStatusHandle.AggregateId.ShouldBe("tenant.alpha");
    }

    [Fact]
    public void Confirmed_remove_dispatches_once_through_the_composed_detail_page()
    {
        // The flow-only tests hold IsCommandSurfaceAvailable at its default, so they cannot observe the
        // parameter the page flips to false while granting the aggregate lease. Only the composed surface
        // proves that acquiring the lease does not invalidate the submit it was acquired for.
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        TenantDetail detail = Detail("tenant.alpha");
        queryGateway.SupportsRemoveConfigurationPreview.Returns(true);
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                detail,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(detail) with { ProjectionVersion = "tenant-sequence:41" }));
        queryGateway.GetRemoveConfigurationPreviewAsync(
                Arg.Any<TenantRemoveConfigurationIntent>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantRemoveConfigurationPreview.Create(
                call.ArgAt<TenantRemoveConfigurationIntent>(0),
                TenantStatus.Active,
                TenantRemoveConfigurationCurrentState.Present,
                ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41",
                isAuthorized: true)));
        queryGateway.GetRemoveConfigurationProjectionProofAsync(
                Arg.Any<TenantRemoveConfigurationIntent>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantConfigurationProjectionProof.Create(
                "tenant.alpha",
                TenantConfigurationProjectionProofKind.RemoveNotConfirmed,
                "tenant-sequence:41",
                call.ArgAt<TenantRemoveConfigurationIntent>(0).AttemptFingerprint)));
        ConfigureAuthoritativeAuditCapability(queryGateway);
        var tracker = new TenantRemoveConfigurationAttemptTracker();
        var gate = new TenantAggregateCommandAdmissionGate();
        var commandGateway = new TrackingRemoveConfigurationCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        Services.RemoveAll<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton(gate);
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton(tracker);
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-config-management-remove-open']"));
        cut.Find("[data-testid='tenants-config-management-remove-open']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-config-remove-confirmation']"));
        cut.Find("[data-testid='tenants-config-remove-confirmation']").Change("billing.mode");
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-config-remove-submit']")
            .HasAttribute("disabled").ShouldBeFalse());
        cut.Find("[data-testid='tenants-config-remove-flow'] form").Submit();

        cut.WaitForAssertion(() => commandGateway.RemoveCallCount.ShouldBe(1));
        cut.FindComponent<RemoveTenantConfigurationFlow>().Instance.Snapshot.RetainsAttempt.ShouldBeTrue();
        tracker.HasPendingOwnership("tenant.alpha").ShouldBeTrue();
    }

    [Fact]
    public async Task Matching_signalr_notification_reconciles_retained_remove_without_redispatch_or_nudge_success()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backendSubscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        TenantDetail detail = Detail("tenant.alpha");
        queryGateway.SupportsRemoveConfigurationPreview.Returns(true);
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                detail,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(detail) with { ProjectionVersion = "tenant-sequence:41" }));
        TenantRemoveConfigurationIntent intent = new("tenant.alpha", "billing", "billing.mode");
        int proofReads = 0;
        queryGateway.GetRemoveConfigurationProjectionProofAsync(intent, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                int read = Interlocked.Increment(ref proofReads);
                return Task.FromResult(TenantConfigurationProjectionProof.Create(
                    intent.TenantId,
                    read == 1
                        ? TenantConfigurationProjectionProofKind.RemoveNotConfirmed
                        : TenantConfigurationProjectionProofKind.RemoveConfirmed,
                    read == 1 ? "tenant-sequence:41" : "tenant-sequence:42",
                    intent.AttemptFingerprint));
            });
        ConfigureAuthoritativeAuditCapability(queryGateway);
        backendSubscription.SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        var tracker = new TenantRemoveConfigurationAttemptTracker();
        (string messageId, DateTimeOffset startedAt) = tracker.BeginDispatch(
            intent,
            "tenant-sequence:41",
            DateTimeOffset.UtcNow);
        TenantRemoveConfigurationPreview preview = TenantRemoveConfigurationPreview.Create(
            intent,
            TenantStatus.Active,
            TenantRemoveConfigurationCurrentState.Present,
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            "tenant-sequence:41",
            isAuthorized: true);
        tracker.Remember(TenantRemoveConfigurationCommandSnapshot.Idle()
            .Previewed(preview)
            .RequestSent(preview, messageId, startedAt)
            .Accepted(TenantCommandSubmissionResult.Accepted(messageId, messageId))).ShouldBeTrue();
        var commandGateway = new TrackingRemoveConfigurationCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true),
        };
        Services.AddSingleton(queryGateway);
        Services.AddSingleton(backendSubscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton(tracker);
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.FindComponent<RemoveTenantConfigurationFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted));
        int statusReadsBeforeNudge = commandGateway.StatusCallCount;
        int proofReadsBeforeNudge = proofReads;
        await backendSubscription.Received(1)
            .SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>());

        commandGateway.Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 };
        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.alpha");

        cut.WaitForAssertion(() => cut.FindComponent<RemoveTenantConfigurationFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        commandGateway.RemoveCallCount.ShouldBe(0);
        commandGateway.StatusCallCount.ShouldBeGreaterThan(statusReadsBeforeNudge);
        proofReads.ShouldBeGreaterThan(proofReadsBeforeNudge);
    }

    [Fact]
    public void Tracker_expiry_autonomously_releases_remove_gate_while_page_remains_mounted()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        TenantDetail detail = Detail("tenant.alpha");
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                detail,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(detail) with { ProjectionVersion = "tenant-sequence:41" }));
        ConfigureAuthoritativeAuditCapability(queryGateway);
        var tracker = new TenantRemoveConfigurationAttemptTracker();
        DateTimeOffset startedAt = DateTimeOffset.UtcNow
            - TenantRemoveConfigurationCommandSnapshot.MaximumRetainedAttemptDuration
            + TimeSpan.FromMilliseconds(500);
        _ = tracker.BeginDispatch(
            new TenantRemoveConfigurationIntent("tenant.alpha", "billing", "billing.mode"),
            "tenant-sequence:41",
            startedAt);
        var gate = new TenantAggregateCommandAdmissionGate();
        string lockKey = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        gate.TryAcquire(lockKey, tracker.LeaseOwner).ShouldBeTrue();
        Services.RemoveAll<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton(gate);
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new TrackingRemoveConfigurationCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton(tracker);
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForAssertion(
            () => gate.IsOwnedBy(lockKey, tracker.LeaseOwner).ShouldBeFalse(),
            TimeSpan.FromSeconds(3));
        tracker.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
    }

    [Fact]
    public async Task Route_change_then_old_flow_terminalization_releases_the_tracker_owned_lifecycle_gate()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ReadyWithSafeConfiguration(
                Detail(call.ArgAt<TenantDetailRequest>(0).TenantId),
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(
                Detail(call.ArgAt<TenantUsersRequest>(0).TenantId)) with
            {
                ProjectionVersion = "tenant-sequence:41",
            }));
        queryGateway.GetLifecycleProjectionProofAsync(
                Arg.Any<TenantLifecycleCommandRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ReadyWithSafeConfiguration(
                Detail(call.ArgAt<TenantLifecycleCommandRequest>(0).TenantId),
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        ConfigureAuthoritativeAuditCapability(queryGateway);
        var tracker = new TenantLifecycleAttemptTracker();
        var gate = new TenantAggregateCommandAdmissionGate();
        var commandGateway = new TrackingLifecycleCommandGateway();
        Services.RemoveAll<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton(gate);
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton(tracker);
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-lifecycle-disable']")
            .GetAttribute("disabled").ShouldBeNull());
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("[data-testid='tenants-lifecycle-command-flow'] form").Submit();
        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending));
        TenantLifecycleCommandFlow oldFlow = cut.FindComponent<TenantLifecycleCommandFlow>().Instance;
        string lockKey = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        gate.IsOwnedBy(lockKey, tracker.LeaseOwner).ShouldBeTrue();
        tracker.Find("tenant.alpha").ShouldNotBeNull();

        cut.Render(parameters => parameters.Add(page => page.TenantId, "tenant.beta"));
        cut.WaitForAssertion(() => cut.Find(".tenant-detail__literal").TextContent.ShouldBe("tenant.beta"));
        gate.IsOwnedBy(lockKey, tracker.LeaseOwner).ShouldBeTrue();

        tracker.Forget("tenant.alpha", oldFlow.Snapshot.MessageId.ShouldNotBeNull());
        tracker.Find("tenant.alpha").ShouldBeNull();
        await cut.InvokeAsync(() => oldFlow.CommandActivityLease.ShouldNotBeNull()(false));

        gate.IsOwnedBy(lockKey, tracker.LeaseOwner).ShouldBeFalse();
        commandGateway.DisableCallCount.ShouldBe(1);
    }

    [Fact]
    public async Task Route_change_retains_dispatch_only_ownership_until_access_observes_deadline_expiry()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ReadyWithSafeConfiguration(
                Detail(call.ArgAt<TenantDetailRequest>(0).TenantId),
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(
                Detail(call.ArgAt<TenantUsersRequest>(0).TenantId)) with
            {
                ProjectionVersion = "tenant-sequence:41",
            }));
        ConfigureAuthoritativeAuditCapability(queryGateway);
        DateTimeOffset now = DateTimeOffset.UtcNow;
        var tracker = new TenantLifecycleAttemptTracker(() => now);
        var gate = new TenantAggregateCommandAdmissionGate();
        Services.RemoveAll<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton(gate);
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new TrackingLifecycleCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton(tracker);
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find(".tenant-detail__literal").TextContent.ShouldBe("tenant.alpha"));
        object lifecycleOwner = typeof(TenantDetailPage)
            .GetField("_lifecycleLeaseOwner", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(cut.Instance)!;
        MethodInfo leaseMethod = typeof(TenantDetailPage)
            .GetMethod("SetCommandActivityLeaseAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        _ = await cut.InvokeAsync(() => (Task<bool>)leaseMethod.Invoke(
            cut.Instance,
            ["tenant.alpha", true, lifecycleOwner])!);
        string lockKey = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        tracker.BeginDispatch(
            new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.DisableTenant),
            "tenant-sequence:41",
            now);

        cut.Render(parameters => parameters.Add(page => page.TenantId, "tenant.beta"));
        cut.WaitForAssertion(() => cut.Find(".tenant-detail__literal").TextContent.ShouldBe("tenant.beta"));
        gate.IsOwnedBy(lockKey, tracker.LeaseOwner).ShouldBeTrue();

        now += TenantLifecycleCommandSnapshot.MaximumRetainedAttemptDuration;
        _ = await cut.InvokeAsync(() => (Task<bool>)leaseMethod.Invoke(
            cut.Instance,
            ["tenant.alpha", false, lifecycleOwner])!);

        tracker.HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        gate.IsOwnedBy(lockKey, tracker.LeaseOwner).ShouldBeFalse();
    }

    [Fact]
    public async Task Disposed_page_old_terminal_callback_does_not_release_a_newer_retained_lifecycle_attempt()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        TenantDetail active = Detail("tenant.alpha");
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                active,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(active) with { ProjectionVersion = "tenant-sequence:41" }));
        queryGateway.GetLifecycleProjectionProofAsync(
                Arg.Any<TenantLifecycleCommandRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                active,
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")));
        ConfigureAuthoritativeAuditCapability(queryGateway);
        var tracker = new TenantLifecycleAttemptTracker();
        var gate = new TenantAggregateCommandAdmissionGate();
        var commandGateway = new TrackingLifecycleCommandGateway();
        Services.RemoveAll<TenantAggregateCommandAdmissionGate>();
        Services.AddSingleton(gate);
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton(tracker);
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-lifecycle-disable']")
            .GetAttribute("disabled").ShouldBeNull());
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("[data-testid='tenants-lifecycle-command-flow'] form").Submit();
        cut.WaitForAssertion(() => cut.FindComponent<TenantLifecycleCommandFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending));
        TenantLifecycleCommandFlow oldFlow = cut.FindComponent<TenantLifecycleCommandFlow>().Instance;
        TenantLifecycleActionAvailability oldAvailability = cut.FindComponent<TenantLifecycleActionAvailability>().Instance;
        string lockKey = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        gate.IsOwnedBy(lockKey, tracker.LeaseOwner).ShouldBeTrue();

        await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        (await oldFlow.CommandActivityLease.ShouldNotBeNull()(true)).ShouldBeFalse();
        tracker.Forget("tenant.alpha", oldFlow.Snapshot.MessageId.ShouldNotBeNull());
        tracker.Find("tenant.alpha").ShouldBeNull();

        var newerIntent = new TenantLifecycleCommandRequest("tenant.alpha", TenantLifecycleOperation.EnableTenant);
        TenantLifecycleCommandSnapshot newer = TenantLifecycleCommandSnapshot
            .Idle(Detail("tenant.alpha", new Dictionary<string, string>(), TenantStatus.Disabled, active.Members))
            .Previewed(
                newerIntent,
                Detail("tenant.alpha", new Dictionary<string, string>(), TenantStatus.Disabled, active.Members),
                "tenant-sequence:42")
            .RequestSent(
                newerIntent,
                Detail("tenant.alpha", new Dictionary<string, string>(), TenantStatus.Disabled, active.Members),
                "tenant-sequence:42",
                "message-newer")
            .Accepted(TenantCommandSubmissionResult.Accepted("message-newer", "correlation-newer")) with
        {
            AttemptStartedAtUtc = DateTimeOffset.UtcNow,
        };
        tracker.Remember(newer).ShouldBeTrue();
        await oldFlow.CommandActivityLease.ShouldNotBeNull()(false);

        gate.IsOwnedBy(lockKey, tracker.LeaseOwner).ShouldBeTrue();
        int statusCallsBeforeDisposedNudge = commandGateway.StatusCallCount;
        await oldAvailability.DisposeAsync();
        await oldAvailability.ApplySignalRNudgeAsync();
        commandGateway.StatusCallCount.ShouldBe(statusCallsBeforeDisposedNudge);

        tracker.Forget("tenant.alpha", "message-newer");
        await oldFlow.CommandActivityLease.ShouldNotBeNull()(false);
        gate.IsOwnedBy(lockKey, tracker.LeaseOwner).ShouldBeFalse();
    }

    [Fact]
    public async Task Route_change_cancels_and_discards_an_old_tenant_lifecycle_proof_before_dispatch()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        var proofEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken observedProofToken = default;
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string tenantId = call.ArgAt<TenantDetailRequest>(0).TenantId;
                return Task.FromResult(ReadyWithSafeConfiguration(
                    Detail(tenantId),
                    ProjectionLifecycleState.Current,
                    "tenant-sequence:41"));
            });
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.ArgAt<TenantUsersRequest>(0).TenantId)) with
            {
                ProjectionVersion = "tenant-sequence:41",
            }));
        queryGateway.GetLifecycleProjectionProofAsync(
                Arg.Any<TenantLifecycleCommandRequest>(),
                Arg.Any<CancellationToken>())
            .Returns(call => WaitForProofCancellationAsync(call.ArgAt<CancellationToken>(1)));
        ConfigureAuthoritativeAuditCapability(queryGateway);
        var commandGateway = new TrackingLifecycleCommandGateway();
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<TenantLifecycleAttemptTracker>();
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-lifecycle-disable']")
            .GetAttribute("disabled").ShouldBeNull());
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        Task pendingSubmit = cut.InvokeAsync(() => cut.Find("[data-testid='tenants-lifecycle-command-flow'] form").Submit());
        await proofEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cut.Render(parameters => parameters.Add(page => page.TenantId, "tenant.beta"));
        await pendingSubmit;

        cut.WaitForAssertion(() => cut.Find(".tenant-detail__literal").TextContent.ShouldBe("tenant.beta"));
        observedProofToken.IsCancellationRequested.ShouldBeTrue();
        commandGateway.DisableCallCount.ShouldBe(0);
        cut.FindAll("[data-testid='tenants-lifecycle-command-flow']").ShouldBeEmpty();

        async Task<TenantDetailSnapshot> WaitForProofCancellationAsync(CancellationToken cancellationToken)
        {
            observedProofToken = cancellationToken;
            proofEntered.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("The canceled lifecycle proof unexpectedly completed.");
        }
    }

    [Fact]
    public void Detail_lifecycle_actions_reauthorize_on_circuit_authentication_transitions()
    {
        var authentication = new MutableAuthenticationStateProvider();
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        RegisterServices(
            _ => Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")),
            composition);

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-lifecycle-disable']")
                .GetAttribute("aria-disabled")
                .ShouldBe("false"));

        // Even an administrator-shaped event is only a transition signal. The strict composition result
        // remains authoritative and revokes the action until it is corroborated again.
        composition.Reflection = TenantLifecycleAuthorizationReflectionState.MissingPermission;
        authentication.Notify(AdministratorPrincipal());
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-lifecycle-disable']")
                .GetAttribute("aria-disabled")
                .ShouldBe("true"));

        composition.Reflection = TenantLifecycleAuthorizationReflectionState.Authorized;
        authentication.Notify(AdministratorPrincipal());
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-lifecycle-disable']")
                .GetAttribute("aria-disabled")
                .ShouldBe("false"));
    }

    [Fact]
    public void Enabled_detail_audit_link_closes_while_new_caller_is_pending()
    {
        var authentication = new MutableAuthenticationStateProvider();
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:41")));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull());

        _ = authentication.NotifyPending();

        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-detail-audit-entrypoint']").ShouldBeEmpty());
        cut.FindAll("[data-testid='tenants-member-row']").ShouldBeEmpty();
    }

    [Fact]
    public void Detail_partial_return_keeps_notice_when_marker_cleanup_fails()
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:41")));
        BunitJSModuleInterop module = JSInterop.SetupModule("./js/tenantsFocus.js");
        JSRuntimeInvocationHandler cleanup = module.SetupVoid("consumeReturnMarkers");
        cleanup.SetException(new Microsoft.JSInterop.JSException("Marker cleanup unavailable."));
        Services.GetRequiredService<NavigationManager>().NavigateTo("/tenants/tenant.alpha?auditPartialReturn=true");

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForAssertion(() => cleanup.Invocations.Count.ShouldBe(1));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        cut.Find("[data-testid='tenants-audit-partial-return']");
    }

    [Theory]
    [InlineData("-tenant")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiJvcGVyYXRvciJ9.c2lnbmF0dXJl")]
    public async Task Unsafe_direct_detail_route_performs_no_read_audit_probe_or_subscription(string tenantId)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backendSubscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider());
        Services.AddSingleton(queryGateway);
        Services.AddSingleton(backendSubscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, tenantId));
        cut.WaitForElement("[data-testid='tenants-detail-error']");
        cut.Render();

        await backendSubscription.DidNotReceiveWithAnyArgs().SubscribeAsync(default!, default!, default);
        await queryGateway.DidNotReceiveWithAnyArgs().GetTenantAsync(default!, default, default);
        await queryGateway.DidNotReceiveWithAnyArgs().GetTenantAuditAsync(default!, default, default);
        cut.Markup.ShouldNotContain(tenantId);
    }

    [Fact]
    public void Detail_lifecycle_command_audit_link_uses_the_page_availability_and_return_cascades()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        TenantDetail active = Detail("tenant.alpha");
        queryGateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(ReadyWithSafeConfiguration(active, ProjectionLifecycleState.Current, "tenant-sequence:41")));
        queryGateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(active) with { ProjectionVersion = "tenant-sequence:41" }));
        queryGateway.GetLifecycleProjectionProofAsync(Arg.Any<TenantLifecycleCommandRequest>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(ReadyWithSafeConfiguration(active, ProjectionLifecycleState.Current, "tenant-sequence:41")));
        ConfigureAuthoritativeAuditCapability(queryGateway);
        // Events are stored, so the audit dimension is pending and the shared control hosts the command
        // audit entry point. Acceptance alone implies no audit state and renders no audit control.
        var commandGateway = new TrackingLifecycleCommandGateway
        {
            Status = new TenantCommandStatusResult(CommandStatus.EventsStored, HasVerifiedCommandIdentity: true),
        };
        Services.AddSingleton<AuthenticationStateProvider>(new MutableAuthenticationStateProvider());
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(
            new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized));
        Services.AddSingleton<TenantLifecycleAttemptTracker>();
        Services.AddFluentUIComponents();
        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants/tenant.alpha?returnUrl=%2Ftenants%3Fsearch%3Dalpha");

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-lifecycle-disable']")
            .GetAttribute("disabled").ShouldBeNull());
        cut.Find("[data-testid='tenants-lifecycle-disable']").Click();
        cut.Find("[data-testid='tenants-lifecycle-confirmation']").Change("tenant.alpha");
        cut.Find("[data-testid='tenants-lifecycle-command-flow'] form").Submit();

        string href = cut.WaitForElement("[data-testid='tenants-command-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull();
        Uri.UnescapeDataString(href).ShouldBe(
            "/tenants/tenant.alpha/audit?source=command-result&returnUrl=/tenants/tenant.alpha?returnUrl=%2Ftenants%3Fsearch%3Dalpha&returnFocus=tenants-lifecycle-lifecycle");
    }

    [Fact]
    public void Detail_audit_waits_for_initial_caller_before_capability_read()
    {
        var pending = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
        AuthenticationStateProvider caller = Substitute.For<AuthenticationStateProvider>();
        caller.GetAuthenticationStateAsync().Returns(pending.Task);
        Services.AddSingleton(caller);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:41")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-loading']");
        _ = gateway.DidNotReceive().GetTenantAuditAsync(
            Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>());

        pending.SetResult(new AuthenticationState(AdministratorPrincipal()));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull());
    }

    [Fact]
    public void Synchronous_initial_caller_failure_requires_reacquisition_before_detail_capability()
    {
        AuthenticationStateProvider caller = Substitute.For<AuthenticationStateProvider>();
        int callerReads = 0;
        caller.GetAuthenticationStateAsync().Returns(_ => ++callerReads == 1
            ? throw new InvalidOperationException("Authentication is unavailable.")
            : Task.FromResult(new AuthenticationState(AdministratorPrincipal())));
        Services.AddSingleton(caller);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:41")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-unauthorized']");
        _ = gateway.DidNotReceive().GetTenantAuditAsync(
            Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>());
        cut.FindAll("[data-testid='tenants-detail-audit-entrypoint']").ShouldBeEmpty();

        cut.Find("[data-testid='tenants-detail-refresh']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull());
        callerReads.ShouldBeGreaterThanOrEqualTo(2);
        _ = gateway.Received(1).GetTenantAuditAsync(
            Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Detail_without_authentication_provider_keeps_audit_closed()
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:41")));
        Services.RemoveAll<AuthenticationStateProvider>();
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-unauthorized']");
        cut.FindAll("fluent-anchor-button[data-testid='tenants-audit-entrypoint']").ShouldBeEmpty();
        _ = gateway.DidNotReceive().GetTenantAuditAsync(
            Arg.Any<TenantAuditRequest>(), Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Unauthorized_page_one_audit_probe_hides_detail_and_member_entries()
    {
        AuthenticationStateProvider caller = Substitute.For<AuthenticationStateProvider>();
        caller.GetAuthenticationStateAsync().Returns(Task.FromResult(new AuthenticationState(AdministratorPrincipal())));
        Services.AddSingleton(caller);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:1")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();
        gateway.GetTenantAuditAsync(
                Arg.Is<TenantAuditRequest>(request => request.PageSize == 1),
                Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => TenantAuditSnapshot.Unauthorized(call.ArgAt<TenantAuditRequest>(0)));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        cut.WaitForElement("[data-testid='tenants-member-row']");
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-detail-audit-entrypoint']").ShouldBeEmpty());
        cut.FindAll("[data-testid='tenants-member-audit-entrypoint']").ShouldBeEmpty();
        _ = gateway.Received(1).GetTenantAuditAsync(
            Arg.Is<TenantAuditRequest>(request => request.PageSize == 1),
            Arg.Any<TenantAuditSnapshot?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Detail_audit_refresh_waits_for_pending_caller_before_probing_again()
    {
        var authentication = new MutableAuthenticationStateProvider();
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:41")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull());

        TaskCompletionSource<AuthenticationState> pending = authentication.NotifyPending();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-detail-audit-entrypoint']").ShouldBeEmpty());
        await cut.InvokeAsync(() => (Task)typeof(TenantDetailPage).GetMethod(
            "RefreshAuditCapabilityAsync", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cut.Instance, null)!);
        cut.FindAll("[data-testid='tenants-detail-audit-entrypoint']").ShouldBeEmpty();
        _ = gateway.Received(1).GetTenantAuditAsync(
            Arg.Is<TenantAuditRequest>(request => request.PageSize == 1),
            Arg.Any<TenantAuditSnapshot?>(),
            Arg.Any<CancellationToken>());

        pending.SetResult(new AuthenticationState(AdministratorPrincipal()));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull());
        _ = gateway.Received(2).GetTenantAuditAsync(
            Arg.Is<TenantAuditRequest>(request => request.PageSize == 1),
            Arg.Any<TenantAuditSnapshot?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Detail_audit_refresh_after_failed_caller_reacquires_caller_and_capability()
    {
        var authentication = new MutableAuthenticationStateProvider();
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:41")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull());

        TaskCompletionSource<AuthenticationState> pending = authentication.NotifyPending();
        pending.SetException(new InvalidOperationException("Authentication failed."));
        cut.WaitForElement("[data-testid='tenants-detail-error']");
        cut.Find("[data-testid='tenants-detail-refresh']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull());
        cut.WaitForElement("[data-testid='tenants-detail-audit-origin'][data-audit-focus-terminal]");
        _ = gateway.Received(2).GetTenantAuditAsync(
            Arg.Is<TenantAuditRequest>(request => request.PageSize == 1),
            Arg.Any<TenantAuditSnapshot?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Rendered_list_detail_audit_and_back_links_retain_safe_list_context()
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:41")));
        TenantWorkspaceState listState = TenantWorkspaceState.FromQuery(
            TenantWorkspaceState.TenantsTab, TenantWorkspaceState.AllScope,
            userId: null, search: "alpha", status: null, sort: null, sortDescending: null,
            cursor: null, selectedTenantId: null, anchor: null);
        TenantListNavigationContext navigation = new(listState);
        IRenderedComponent<TenantDataGrid> list = Render<TenantDataGrid>(parameters => parameters
            .Add(grid => grid.Rows, [TenantListRow.FromSummary(new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active))])
            .Add(grid => grid.DetailHref, navigation.ToDetailUrl)
            .Add(grid => grid.AuditHref, navigation.ToAuditUrl));
        string detailHref = list.Find("[data-testid='tenants-list-detail-link']").GetAttribute("href").ShouldNotBeNull();
        detailHref.ShouldStartWith("/tenants/tenant.alpha?returnUrl=");
        string listReturn = QueryValue(detailHref, "returnUrl");
        listReturn.ShouldContain("search=alpha");
        listReturn.ShouldContain("selected=tenant.alpha");

        Services.GetRequiredService<NavigationManager>().NavigateTo(detailHref);
        IRenderedComponent<TenantDetailPage> detail = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        detail.WaitForAssertion(() => detail.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull());
        string auditHref = detail.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull();
        auditHref.ShouldStartWith("/tenants/tenant.alpha/audit?");

        QueryValue(auditHref, "returnFocus").ShouldBe("tenants-detail-identity");
        Services.GetRequiredService<NavigationManager>().NavigateTo(auditHref);
        IRenderedComponent<TenantAuditPage> audit = Render<TenantAuditPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        string auditBack = audit.Find("[data-testid='tenants-audit-back']").GetAttribute("href").ShouldNotBeNull();
        auditBack.ShouldStartWith("/tenants/tenant.alpha?returnUrl=");
        QueryValue(auditBack, "auditFocus").ShouldBe("tenants-detail-identity");

        Services.GetRequiredService<NavigationManager>().NavigateTo(auditBack);
        IRenderedComponent<TenantDetailPage> returnedDetail = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        returnedDetail.WaitForElement("[data-testid='tenants-detail-back']");
        returnedDetail.Find("[data-testid='tenants-detail-back']")
            .GetAttribute("href").ShouldBe(listReturn);

        static string QueryValue(string href, string key)
        {
            string pair = new Uri("http://localhost" + href).Query.TrimStart('?').Split('&')
                .Single(part => part.StartsWith(key + "=", StringComparison.Ordinal));
            return Uri.UnescapeDataString(pair[(key.Length + 1)..]);
        }
    }

    [Fact]
    public void Detail_handoff_reports_stripped_list_cursor_on_audit_and_list_returns()
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:41")));
        Services.GetRequiredService<NavigationManager>().NavigateTo(
            "/tenants/tenant.alpha?returnUrl=%2Ftenants%3Fcursor%3Dopaque-next%26search%3Dalpha");

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull());

        string auditHref = cut.Find("[data-testid='tenants-detail-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldNotBeNull();
        auditHref.ShouldContain("auditPartialReturn%3Dtrue");
        auditHref.ShouldNotContain("cursor");
        cut.Find("[data-testid='tenants-detail-back']").GetAttribute("href")
            .ShouldBe("/tenants?search=alpha&auditPartialReturn=true");
    }

    /// <summary>
    /// Lifecycle authorization sets Indeterminate before the async resolve completes. Without that fail-closed
    /// window, actions could render as available while evidence was still pending.
    /// </summary>
    [Fact]
    public async Task Detail_lifecycle_actions_fail_closed_while_authorization_is_pending()
    {
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        composition.ResolutionGate = gate;
        RegisterServices(
            _ => Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")),
            composition);

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        await composition.ResolutionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cut.Find("[data-testid='tenants-lifecycle-disable']")
            .GetAttribute("aria-disabled")
            .ShouldBe("true");

        gate.SetResult();
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-lifecycle-disable']")
                .GetAttribute("aria-disabled")
                .ShouldBe("false"));
    }

    /// <summary>
    /// A superseded lifecycle authorization generation must not overwrite a newer transition result.
    /// </summary>
    [Fact]
    public async Task Detail_lifecycle_authorization_discards_a_superseded_resolution()
    {
        var authentication = new MutableAuthenticationStateProvider();
        var composition = new StubTenantsBffComposition(TenantLifecycleAuthorizationReflectionState.Authorized);
        var firstGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        composition.ResolutionGate = firstGate;
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        RegisterServices(
            _ => Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "tenant-sequence:41")),
            composition);

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        await composition.ResolutionEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        composition.Reflection = TenantLifecycleAuthorizationReflectionState.MissingPermission;
        var secondGate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        composition.ResolutionGate = secondGate;
        authentication.Notify(AdministratorPrincipal());
        await WaitUntilAsync(() => composition.AsyncResolutionCount >= 2, TimeSpan.FromSeconds(5));
        secondGate.SetResult();
        cut.WaitForAssertion(() =>
            cut.Find("[data-testid='tenants-lifecycle-disable']")
                .GetAttribute("aria-disabled")
                .ShouldBe("true"));

        composition.Reflection = TenantLifecycleAuthorizationReflectionState.Authorized;
        firstGate.SetResult();
        await Task.Delay(250);

        cut.Find("[data-testid='tenants-lifecycle-disable']")
            .GetAttribute("aria-disabled")
            .ShouldBe("true");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Detail_page_renders_unnamed_fallback_heading_for_blank_name_without_crashing(string blankName)
    {
        // Regression guard for P-DN1: a tenant persisted with a blank Name must not crash the page.
        // FcPageHeader.OnParametersSet throws ArgumentException on a blank Heading, so the success
        // branch supplies the localized "Unnamed tenant" fallback instead of binding the empty name.
        // Rendering the success identity surface at all proves FcPageHeader did not throw.
        TenantDetail unnamed = new(
            "tenant.alpha",
            blankName,
            "Tenant alpha description",
            TenantStatus.Active,
            [
                new TenantMember("owner-user", TenantRole.TenantOwner),
                new TenantMember("reader-user", TenantRole.TenantReader),
            ],
            new Dictionary<string, string> { ["billing.mode"] = "trial" },
            DateTimeOffset.Parse("2026-06-01T12:00:00Z", CultureInfo.InvariantCulture));
        RegisterServices(_ => Task.FromResult(
            TenantDetailSnapshot.Ready(unnamed, "\"etag\"", ReadModelFreshnessState.Current).WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha"))));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");

        string expectedFallback = Services
            .GetRequiredService<IStringLocalizer<TenantsResources>>()["Tenants.Detail.UnnamedTenant"];
        IElement heading = cut.Find("h1[id='tenants-detail-identity']");
        heading.TextContent.ShouldNotBeNullOrWhiteSpace();
        heading.TextContent.Trim().ShouldBe(expectedFallback);
    }

    [Fact]
    public void Detail_page_displays_loading_until_gateway_completes()
    {
        TaskCompletionSource<TenantDetailSnapshot> detailResult = new(TaskCreationOptions.RunContinuationsAsynchronously);
        RegisterServices(_ => detailResult.Task);

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        cut.Find("[data-testid='tenants-detail-loading']").TextContent.ShouldContain("loading", Case.Insensitive);

        detailResult.SetResult(TenantDetailSnapshot.Ready(Detail("tenant.alpha"), "\"etag\"", ReadModelFreshnessState.Current).WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha")));

        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        cut.Find("[data-testid='tenants-detail-identity']").TextContent.ShouldContain("tenant.alpha");
    }

    [Fact]
    public async Task Route_change_cancels_and_discards_the_obsolete_tenant_completion()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        var alphaResult = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken alphaCancellation = default;
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantDetailRequest request = call.Arg<TenantDetailRequest>()
                    ?? throw new InvalidOperationException("A tenant detail request is required.");
                if (request.TenantId == "tenant.alpha")
                {
                    alphaCancellation = call.ArgAt<CancellationToken>(2);
                    return alphaResult.Task;
                }

                return Task.FromResult(TenantDetailSnapshot.Ready(
                    Detail("tenant.beta"),
                    "beta-etag",
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current,
                    "projection-v1").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha")));
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantUsersRequest request = call.Arg<TenantUsersRequest>()
                    ?? throw new InvalidOperationException("A tenant-users request is required.");
                return Task.FromResult(TenantUsersSnapshot.Ready(
                    request.TenantId,
                    [new TenantMember(request.TenantId + "-member", TenantRole.TenantReader)],
                    nextCursor: null,
                    hasMore: false,
                    eTag: request.TenantId + "-members-etag",
                    projectionVersion: "projection-v1",
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") });
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.Find("[data-testid='tenants-detail-loading']");

        cut.Render(parameters => parameters.Add(page => page.TenantId, "tenant.beta"));
        cut.WaitForAssertion(() =>
        {
            alphaCancellation.IsCancellationRequested.ShouldBeTrue();
            cut.Find(".tenant-detail__literal").TextContent.ShouldBe("tenant.beta");
            cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("tenant.beta-member");
        });

        alphaResult.SetResult(TenantDetailSnapshot.Ready(
            Detail("tenant.alpha"),
            "alpha-etag",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            "projection-v1").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha")));
        await Task.Yield();

        cut.Find(".tenant-detail__literal").TextContent.ShouldBe("tenant.beta");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldNotContain("tenant.alpha-member");
    }

    [Fact]
    public async Task Route_change_rebinds_the_notification_lease_to_the_new_tenant()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backendSubscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        backendSubscription
            .SubscribeAsync("tenants", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        backendSubscription
            .UnsubscribeAsync("tenants", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string tenantId = call.Arg<TenantDetailRequest>()!.TenantId;
                return Task.FromResult(ReadyWithSafeConfiguration(
                    Detail(tenantId),
                    ProjectionLifecycleState.Current,
                    "projection-v1"));
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>()!.TenantId))));
        Services.AddSingleton(gateway);
        Services.AddSingleton(backendSubscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        await backendSubscription.Received(1)
            .SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>());

        cut.Render(parameters => parameters.Add(page => page.TenantId, "tenant.beta"));
        cut.WaitForAssertion(() => cut.Find(".tenant-detail__literal").TextContent.ShouldBe("tenant.beta"));

        await backendSubscription.Received(1)
            .UnsubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>());
        await backendSubscription.Received(1)
            .SubscribeAsync("tenants", "tenant.beta", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Matching_notification_retains_member_rows_and_triggers_only_the_authoritative_tenant_reads()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backendSubscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        backendSubscription
            .SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        TenantDetailSnapshot initialDetail = ReadyWithSafeConfiguration(
            Detail("tenant.alpha"),
            ProjectionLifecycleState.Current,
            "projection-v1");
        TenantUsersSnapshot initialMembers = TenantUsersSnapshot.Ready(
            "tenant.alpha",
            [new TenantMember("last-confirmed-user", TenantRole.TenantReader)],
            nextCursor: null,
            hasMore: false,
            eTag: "members-v1-etag",
            projectionVersion: "projection-v1",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };
        var refreshedDetail = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshedMembers = new TaskCompletionSource<TenantUsersSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        List<TenantDetailRequest> detailRequests = [];
        List<TenantUsersRequest> memberRequests = [];

        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                detailRequests.Add(call.Arg<TenantDetailRequest>()!);
                return detailRequests.Count == 1 ? Task.FromResult(initialDetail) : refreshedDetail.Task;
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                memberRequests.Add(call.Arg<TenantUsersRequest>()!);
                return memberRequests.Count == 1 ? Task.FromResult(initialMembers) : refreshedMembers.Task;
            });

        Services.AddSingleton(gateway);
        Services.AddSingleton(backendSubscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-table']");
        await backendSubscription.Received(1)
            .SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>());

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.beta");
        await Task.Yield();
        detailRequests.Count.ShouldBe(1);
        memberRequests.Count.ShouldBe(1);

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.alpha");
        cut.WaitForAssertion(() =>
        {
            detailRequests.Count.ShouldBe(2);
            memberRequests.Count.ShouldBe(2);
            cut.Find("[data-testid='tenants-member-refreshing']");
            cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("last-confirmed-user");
        });

        detailRequests[1].ETag.ShouldBe(initialDetail.ETag);
        memberRequests[1].ETag.ShouldBe(initialMembers.ETag);
        memberRequests[1].Cursor.ShouldBeNull();

        refreshedDetail.SetResult(initialDetail);
        refreshedMembers.SetResult(TenantUsersSnapshot.Ready(
            "tenant.alpha",
            [new TenantMember("authoritative-refreshed-user", TenantRole.TenantReader)],
            nextCursor: null,
            hasMore: false,
            eTag: "members-v2-etag",
            projectionVersion: "projection-v1",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") });

        cut.WaitForAssertion(() =>
        {
            cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("authoritative-refreshed-user");
            cut.FindAll("[data-testid='tenants-member-refreshing']").ShouldBeEmpty();
        });
    }

    [Fact]
    public async Task Faulted_member_refresh_clears_refreshing_and_retains_last_confirmed_rows_as_degraded()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        TenantDetailSnapshot detail = ReadyWithSafeConfiguration(
            Detail("tenant.alpha"),
            ProjectionLifecycleState.Current,
            "projection-v1");
        TenantUsersSnapshot members = TenantUsersSnapshot.Ready(
            "tenant.alpha",
            [new TenantMember("last-confirmed-user", TenantRole.TenantReader)],
            nextCursor: null,
            hasMore: false,
            eTag: "members-v1-etag",
            projectionVersion: "projection-v1",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };
        int memberReads = 0;
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(detail));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref memberReads) == 1
                ? Task.FromResult(members)
                : Task.FromException<TenantUsersSnapshot>(new InvalidOperationException("transport detail must stay contained")));

        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-table']");

        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            MemberAccessReview memberReview = cut.FindComponent<MemberAccessReview>().Instance;
            memberReview.Members.Kind.ShouldBe(TenantUsersSurfaceKind.Degraded);
            memberReview.Members.Reason.ShouldBe(TenantUsersReason.GatewayFailure);
            cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("last-confirmed-user");
            cut.FindAll("[data-testid='tenants-member-refreshing']").ShouldBeEmpty();
        });
    }

    [Fact]
    public void Confirmed_member_command_refreshes_parent_detail_and_member_projections()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        int detailReads = 0;
        int memberReads = 0;
        TenantDetail initialDetail = Detail("tenant.alpha");
        TenantDetail refreshedDetail = Detail(
            "tenant.alpha",
            new Dictionary<string, string>(),
            TenantStatus.Active,
            [
                .. initialDetail.Members,
                new TenantMember("new-member", TenantRole.TenantReader),
            ]);
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                int call = Interlocked.Increment(ref detailReads);
                return Task.FromResult(ReadyWithSafeConfiguration(
                    call <= 3 ? initialDetail : refreshedDetail,
                    ProjectionLifecycleState.Current,
                    call <= 3 ? "projection-v1" : "projection-v2"));
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                int call = Interlocked.Increment(ref memberReads);
                return Task.FromResult(MemberSnapshot(call == 1 ? initialDetail : refreshedDetail) with
                {
                    ProjectionVersion = call == 1 ? "projection-v1" : "projection-v2",
                });
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway
        {
            AddMemberSubmission = TenantCommandSubmissionResult.Accepted(
                "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                "correlation-456"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 },
        });
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-add-member-user-id']").Change("new-member");
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-add-member-role", nameof(TenantRole.TenantReader));
        cut.Find("[data-testid='tenants-add-member-flow'] form").Submit();

        cut.WaitForAssertion(() =>
        {
            // Three original reads plus fresh authority before admission and after the lease.
            detailReads.ShouldBe(5);
            memberReads.ShouldBe(2);
            cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("new-member");
            cut.FindComponent<AddTenantMemberFlow>().Instance.Snapshot.State
                .ShouldBe(TenantCommandLifecycleState.Confirmed);
        });
    }

    [Fact]
    public void Missing_aggregate_admission_gate_fails_membership_dispatch_closed()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ServiceDescriptor gateDescriptor = Services.Single(static descriptor =>
            descriptor.ServiceType == typeof(TenantAggregateCommandAdmissionGate));
        _ = Services.Remove(gateDescriptor);
        var commandGateway = new TrackingMembershipCommandGateway(
            Task.FromResult(TenantCommandSubmissionResult.Accepted(
                "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                "correlation-456")));
        RegisterMembershipPageServices(commandGateway, new StubTenantsBffComposition());

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-add-member-user-id']").Change("new-member");
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-add-member-role", nameof(TenantRole.TenantReader));

        cut.Find("[data-testid='tenants-add-member-submit']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-add-member-unavailable-reason']").TextContent
            .ShouldContain("command support is unavailable", Case.Insensitive);
        cut.Find("[data-testid='tenants-add-member-flow'] form").Submit();
        commandGateway.AddMemberCallCount.ShouldBe(0);
    }

    [Fact]
    public async Task Reacquiring_the_page_owned_aggregate_lease_is_idempotent_and_keeps_the_release_key()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ServiceDescriptor gateDescriptor = Services.Single(static descriptor =>
            descriptor.ServiceType == typeof(TenantAggregateCommandAdmissionGate));
        _ = Services.Remove(gateDescriptor);
        var admissionGate = new TenantAggregateCommandAdmissionGate();
        Services.AddSingleton(admissionGate);
        RegisterMembershipPageServices(
            new TrackingMembershipCommandGateway(Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."))),
            new StubTenantsBffComposition());
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        Func<bool, Task<bool>> lease = cut.FindComponent<AddTenantMemberFlow>().Instance.CommandActivityLease.ShouldNotBeNull();
        const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        object owner = typeof(TenantDetailPage).GetField("_commandLockOwner", Flags)!.GetValue(cut.Instance).ShouldNotBeNull();
        string lockKey = TenantCommandAggregateLock.ForTenant("tenant.alpha");
        admissionGate.TryAcquire(lockKey, owner).ShouldBeTrue();
        typeof(TenantDetailPage).GetField("_activeAggregateLockKey", Flags)!.SetValue(cut.Instance, lockKey);
        // The lease is owner-aware: re-entry is idempotent only for the surface that holds it, so the held
        // state must name that holder. Setting the key alone models a lease held by nobody, which is now
        // correctly refused rather than silently shared.
        object membershipOwner = typeof(TenantDetailPage).GetField("_membershipLeaseOwner", Flags)!
            .GetValue(cut.Instance).ShouldNotBeNull();
        typeof(TenantDetailPage).GetField("_aggregateLeaseHolder", Flags)!.SetValue(cut.Instance, membershipOwner);

        bool duplicate = await cut.InvokeAsync(() => lease(true));

        duplicate.ShouldBeTrue();
        admissionGate.IsLocked(lockKey).ShouldBeTrue();

        _ = await cut.InvokeAsync(() => lease(false));
        admissionGate.IsLocked(lockKey).ShouldBeFalse();
    }

    [Fact]
    public async Task A_second_command_surface_cannot_share_or_release_the_aggregate_lease_of_another()
    {
        // Regression for the multi-owner lease: the re-acquire branch used to grant the lease to any surface
        // once the key matched, so whichever surface finished first released the aggregate while the other
        // command was still in flight -- breaking one-at-a-time exclusivity (AD-12).
        JSInterop.Mode = JSRuntimeMode.Loose;
        ServiceDescriptor gateDescriptor = Services.Single(static descriptor =>
            descriptor.ServiceType == typeof(TenantAggregateCommandAdmissionGate));
        _ = Services.Remove(gateDescriptor);
        var admissionGate = new TenantAggregateCommandAdmissionGate();
        Services.AddSingleton(admissionGate);
        RegisterMembershipPageServices(
            new TrackingMembershipCommandGateway(Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."))),
            new StubTenantsBffComposition());
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        Func<bool, Task<bool>> membershipLease = cut.FindComponent<AddTenantMemberFlow>()
            .Instance.CommandActivityLease.ShouldNotBeNull();
        const System.Reflection.BindingFlags Flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
        string lockKey = TenantCommandAggregateLock.ForTenant("tenant.alpha");

        (await cut.InvokeAsync(() => membershipLease(true))).ShouldBeTrue();
        admissionGate.IsLocked(lockKey).ShouldBeTrue();

        // A different surface on the same page asks for the same aggregate while membership still holds it.
        object metadataOwner = typeof(TenantDetailPage).GetField("_metadataLeaseOwner", Flags)!
            .GetValue(cut.Instance).ShouldNotBeNull();
        System.Reflection.MethodInfo leaseMethod = typeof(TenantDetailPage)
            .GetMethod("SetCommandActivityLeaseAsync", Flags)!;
        bool sharedByOtherSurface = await cut.InvokeAsync(
            () => (Task<bool>)leaseMethod.Invoke(cut.Instance, ["tenant.alpha", true, metadataOwner])!);

        sharedByOtherSurface.ShouldBeFalse();

        // The refused surface reporting completion must not free a lock it never held.
        _ = await cut.InvokeAsync(
            () => (Task<bool>)leaseMethod.Invoke(cut.Instance, ["tenant.alpha", false, metadataOwner])!);
        admissionGate.IsLocked(lockKey).ShouldBeTrue();

        // The real holder still releases it.
        _ = await cut.InvokeAsync(() => membershipLease(false));
        admissionGate.IsLocked(lockKey).ShouldBeFalse();
    }

    [Fact]
    public async Task Changing_routes_releases_the_previous_page_owned_lease_and_does_not_disable_the_new_tenant()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ServiceDescriptor gateDescriptor = Services.Single(static descriptor =>
            descriptor.ServiceType == typeof(TenantAggregateCommandAdmissionGate));
        _ = Services.Remove(gateDescriptor);
        var admissionGate = new TenantAggregateCommandAdmissionGate();
        Services.AddSingleton(admissionGate);
        RegisterMembershipPageServices(
            new TrackingMembershipCommandGateway(Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."))),
            new StubTenantsBffComposition());
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        Func<bool, Task<bool>> lease = cut.FindComponent<AddTenantMemberFlow>().Instance.CommandActivityLease.ShouldNotBeNull();
        _ = await cut.InvokeAsync(() => lease(true));
        admissionGate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();

        cut.Render(parameters => parameters.Add(page => page.TenantId, "tenant.beta"));

        cut.WaitForAssertion(() =>
        {
            admissionGate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
            cut.Find("[data-testid='tenants-add-member-submit']").GetAttribute("disabled").ShouldBeNull();
        });

        Func<bool, Task<bool>> betaLease = cut.FindComponent<AddTenantMemberFlow>().Instance.CommandActivityLease.ShouldNotBeNull();
        bool betaAccepted = await cut.InvokeAsync(() => betaLease(true));
        betaAccepted.ShouldBeTrue();
        admissionGate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.beta")).ShouldBeTrue();
        _ = await cut.InvokeAsync(() => lease(false));

        admissionGate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.beta")).ShouldBeTrue();
    }

    [Fact]
    public void Disconnected_bff_fails_membership_dispatch_closed_with_inline_reason()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var commandGateway = new TrackingMembershipCommandGateway(
            Task.FromResult(TenantCommandSubmissionResult.Accepted(
                "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                "correlation-456")));
        RegisterMembershipPageServices(
            commandGateway,
            new StubTenantsBffComposition(commandSurfaceConnected: false));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-add-member-user-id']").Change("new-member");
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-add-member-role", nameof(TenantRole.TenantReader));

        cut.Find("[data-testid='tenants-add-member-submit']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-add-member-unavailable-reason']").TextContent
            .ShouldContain("command support is unavailable", Case.Insensitive);
        cut.Find("[data-testid='tenants-add-member-flow'] form").Submit();
        commandGateway.AddMemberCallCount.ShouldBe(0);
    }

    [Fact]
    public void Same_aggregate_membership_command_makes_sibling_membership_surfaces_unavailable()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var admissionGate = new TenantAggregateCommandAdmissionGate();
        TaskCompletionSource<TenantCommandSubmissionResult> pendingSubmission = new();
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(MemberSnapshot(Detail("tenant.alpha")) with
            {
                ProjectionVersion = "projection-v1",
            }));
        Services.AddSingleton(gateway);
        Services.AddSingleton(admissionGate);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new TrackingMembershipCommandGateway(pendingSubmission.Task));
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-add-member-user-id']").Change("new-member");
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-add-member-role", nameof(TenantRole.TenantReader));
        cut.Find("[data-testid='tenants-add-member-flow'] form").Submit();

        cut.WaitForAssertion(() =>
            admissionGate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue());

        cut.WaitForAssertion(() =>
            cut.FindAll("[data-testid='tenants-member-unavailable-reason']")
                .ShouldContain(reason => reason.TextContent.Contains("already in progress for this tenant", StringComparison.OrdinalIgnoreCase)));
        cut.FindAll("[data-testid='tenants-change-role-open']").ShouldBeEmpty();

        pendingSubmission.SetResult(TenantCommandSubmissionResult.Failed("Command submission cancelled by the test."));
    }

    [Fact]
    public async Task Tearing_down_unrelated_member_flow_does_not_release_unresolved_add_submission()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var admissionGate = new TenantAggregateCommandAdmissionGate();
        TaskCompletionSource<TenantCommandSubmissionResult> pendingSubmission = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        int memberReads = 0;
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref memberReads) == 1
                ? Task.FromResult(MemberSnapshot(Detail("tenant.alpha")) with { ProjectionVersion = "projection-v1" })
                : Task.FromResult(TenantUsersSnapshot.Unauthorized("tenant.alpha")));
        Services.AddSingleton(gateway);
        Services.AddSingleton(admissionGate);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new TrackingMembershipCommandGateway(pendingSubmission.Task));
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElements("[data-testid='tenants-change-role-open']")[0].Click();
        cut.Find("[data-testid='tenants-change-role-flow']");
        cut.Find("[data-testid='tenants-add-member-user-id']").Change("new-member");
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-add-member-role", nameof(TenantRole.TenantReader));
        cut.Find("[data-testid='tenants-add-member-flow'] form").Submit();
        cut.WaitForAssertion(() => admissionGate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue());

        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());

        cut.WaitForElement("[data-testid='tenants-member-unauthorized']");
        cut.FindAll("[data-testid='tenants-change-role-flow']").ShouldBeEmpty();
        cut.FindComponent<AddTenantMemberFlow>().Instance.Snapshot.State
            .ShouldBe(TenantCommandLifecycleState.RequestSent);
        admissionGate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();

        pendingSubmission.SetResult(TenantCommandSubmissionResult.Failed("Command submission cancelled by the test."));
        cut.WaitForAssertion(() => admissionGate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse());
    }

    /// <summary>
    /// MemberAccessReview releases the aggregate lease from its own DisposeAsync, but nothing covered the
    /// teardown that motivates it: every existing lease test either keeps the landmark mounted or changes
    /// route (which the page releases instead). When a refresh reclassifies the detail snapshot out of Ready,
    /// the whole ReadyContent subtree -- landmark included -- is replaced while a membership command still
    /// holds the lock, and without DisposeAsync the page latches into a permanently locked command state for
    /// the life of the circuit.
    /// </summary>
    [Fact]
    public async Task UnmountingMemberLandmarkRetainsItsExactAttemptUntilTerminalDelivery()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var admissionGate = new TenantAggregateCommandAdmissionGate();
        TaskCompletionSource<TenantCommandSubmissionResult> pendingSubmission = new(TaskCreationOptions.RunContinuationsAsynchronously);
        int detailReads = 0;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref detailReads) <= 3
                ? Task.FromResult(ReadyWithSafeConfiguration(
                    Detail("tenant.alpha"),
                    ProjectionLifecycleState.Current,
                    "projection-v1"))
                : Task.FromResult(TenantDetailSnapshot.Unauthorized("tenant.alpha")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(MemberSnapshot(Detail("tenant.alpha")) with { ProjectionVersion = "projection-v1" }));
        Services.AddSingleton(gateway);
        Services.AddSingleton(admissionGate);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new TrackingMembershipCommandGateway(pendingSubmission.Task));
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-add-member-user-id']").Change("new-member");
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-add-member-role", nameof(TenantRole.TenantReader));
        cut.Find("[data-testid='tenants-add-member-flow'] form").Submit();
        cut.WaitForAssertion(() => admissionGate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue());

        // The detail read now reclassifies the surface, so ReadyContent -- and the member landmark holding
        // the lease -- is replaced wholesale while the add command is still unresolved.
        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());

        cut.WaitForAssertion(() => cut.FindComponents<MemberAccessReview>().ShouldBeEmpty());
        admissionGate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeTrue();

        pendingSubmission.SetResult(TenantCommandSubmissionResult.Failed("Command submission cancelled by the test."));
        cut.WaitForAssertion(() => admissionGate.IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse());
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
    }

    [Fact]
    public async Task Detail_read_refresh_nudges_in_flight_membership_flow_without_confirming_from_notification()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        int detailReads = 0;
        TenantDetail refreshedDetail = Detail(
            "tenant.alpha",
            new Dictionary<string, string> { ["billing.mode"] = "trial" },
            TenantStatus.Active,
            [
                new TenantMember("owner-user", TenantRole.TenantOwner),
                new TenantMember("reader-user", TenantRole.TenantReader),
                new TenantMember("new-member", TenantRole.TenantReader),
            ]);
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                int call = Interlocked.Increment(ref detailReads);
                return Task.FromResult(ReadyWithSafeConfiguration(
                    call <= 3 ? Detail("tenant.alpha") : refreshedDetail,
                    ProjectionLifecycleState.Current,
                    call <= 3 ? "projection-v1" : "projection-v2"));
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(MemberSnapshot(detailReads <= 3 ? Detail("tenant.alpha") : refreshedDetail) with
            {
                ProjectionVersion = detailReads <= 3 ? "projection-v1" : "projection-v2",
            }));
        var commandGateway = new TrackingMembershipCommandGateway(
            Task.FromResult(TenantCommandSubmissionResult.Accepted("01ARZ3NDEKTSV4RRFFQ69G5FAV", "correlation-456")),
            new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true));
        Services.AddSingleton(gateway);
        Services.AddSingleton(new TenantAggregateCommandAdmissionGate());
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-add-member-user-id']").Change("new-member");
        FluentSelectInterop.ChangeFluentSelect(cut, "tenants-add-member-role", nameof(TenantRole.TenantReader));
        cut.Find("[data-testid='tenants-add-member-flow'] form").Submit();

        cut.WaitForAssertion(() =>
            cut.FindComponent<AddTenantMemberFlow>().Instance.Snapshot.State
                .ShouldBe(TenantCommandLifecycleState.Accepted));

        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());

        cut.WaitForAssertion(() => detailReads.ShouldBeGreaterThan(1));
        cut.FindComponent<AddTenantMemberFlow>().Instance.Snapshot.State
            .ShouldNotBe(TenantCommandLifecycleState.Confirmed);
        commandGateway.AddMemberCallCount.ShouldBe(1);
        commandGateway.StatusCallCount.ShouldBeGreaterThan(1);
    }

    [Theory]
    [InlineData("change-role")]
    [InlineData("remove-member")]
    public async Task Detail_read_refresh_forwards_nudge_to_each_in_flight_membership_flow(string commandKind)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var commandGateway = new TrackingMembershipCommandGateway(
            Task.FromResult(TenantCommandSubmissionResult.Accepted(
                "01ARZ3NDEKTSV4RRFFQ69G5FAV",
                "correlation-456")),
            new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true));
        RegisterMembershipPageServices(commandGateway, new StubTenantsBffComposition());

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        if (commandKind == "change-role")
        {
            cut.WaitForElements("[data-testid='tenants-change-role-open']")[1].Click();
            FluentSelectInterop.ChangeFluentSelect(
                cut,
                "tenants-change-role-new-role",
                nameof(TenantRole.TenantContributor));
            cut.Find("[data-testid='tenants-change-role-flow'] form").Submit();
            cut.WaitForAssertion(() => cut.FindComponent<ChangeTenantMemberRoleFlow>().Instance.Snapshot.State
                .ShouldBe(TenantCommandLifecycleState.Accepted));
        }
        else
        {
            cut.WaitForElements("[data-testid='tenants-remove-member-open']")[1].Click();
            cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
            cut.Find("[data-testid='tenants-remove-member-flow'] form").Submit();
            cut.WaitForAssertion(() => cut.FindComponent<RemoveTenantMemberFlow>().Instance.Snapshot.State
                .ShouldBe(TenantCommandLifecycleState.Accepted));
        }

        int statusCallsBeforeNudge = commandGateway.StatusCallCount;
        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());

        cut.WaitForAssertion(() => commandGateway.StatusCallCount.ShouldBeGreaterThan(statusCallsBeforeNudge));
        if (commandKind == "change-role")
        {
            commandGateway.ChangeRoleCallCount.ShouldBe(1);
            cut.FindComponent<ChangeTenantMemberRoleFlow>().Instance.Snapshot.State
                .ShouldBe(TenantCommandLifecycleState.Accepted);
        }
        else
        {
            commandGateway.RemoveMemberCallCount.ShouldBe(1);
            cut.FindComponent<RemoveTenantMemberFlow>().Instance.Snapshot.State
                .ShouldBe(TenantCommandLifecycleState.Accepted);
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Initial_detail_load_contains_each_fault_and_observes_the_sibling_read(bool faultDetailRead)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        int detailReads = 0;
        int memberReads = 0;
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref detailReads);
                return faultDetailRead
                    ? Task.FromException<TenantDetailSnapshot>(new InvalidOperationException("detail transport detail"))
                    : Task.FromResult(ReadyWithSafeConfiguration(
                        Detail("tenant.alpha"),
                        ProjectionLifecycleState.Current,
                        "projection-v1"));
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                Interlocked.Increment(ref memberReads);
                return faultDetailRead
                    ? Task.FromResult(MemberSnapshot(Detail("tenant.alpha")))
                    : Task.FromException<TenantUsersSnapshot>(new InvalidOperationException("member transport detail"));
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        cut.WaitForElement(faultDetailRead
            ? "[data-testid='tenants-detail-error']"
            : "[data-testid='tenants-member-unavailable']");
        detailReads.ShouldBe(1);
        memberReads.ShouldBe(1);
        if (!faultDetailRead)
        {
            cut.Find("[data-testid='tenants-detail-identity']");
        }
    }

    /// <summary>
    /// Combined refresh must observe each read independently on the refresh path, not only on initial load.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Combined_detail_refresh_contains_each_fault_and_observes_the_sibling_read(bool faultDetailRead)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        TenantDetailSnapshot initialDetail = ReadyWithSafeConfiguration(
            Detail("tenant.alpha"),
            ProjectionLifecycleState.Current,
            "projection-v1");
        TenantUsersSnapshot initialMembers = TenantUsersSnapshot.Ready(
            "tenant.alpha",
            [new TenantMember("last-confirmed-user", TenantRole.TenantReader)],
            nextCursor: null,
            hasMore: false,
            eTag: "members-v1-etag",
            projectionVersion: "projection-v1",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };
        int detailReads = 0;
        int memberReads = 0;
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                int call = Interlocked.Increment(ref detailReads);
                if (call == 1)
                {
                    return Task.FromResult(initialDetail);
                }

                return faultDetailRead
                    ? Task.FromException<TenantDetailSnapshot>(new InvalidOperationException("detail transport detail"))
                    : Task.FromResult(initialDetail);
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                int call = Interlocked.Increment(ref memberReads);
                if (call == 1)
                {
                    return Task.FromResult(initialMembers);
                }

                return faultDetailRead
                    ? Task.FromResult(initialMembers)
                    : Task.FromException<TenantUsersSnapshot>(new InvalidOperationException("member transport detail"));
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        cut.WaitForElement("[data-testid='tenants-member-table']");
        detailReads.ShouldBe(1);
        memberReads.ShouldBe(1);

        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());

        cut.WaitForAssertion(() =>
        {
            detailReads.ShouldBe(2);
            memberReads.ShouldBe(2);
            if (faultDetailRead)
            {
                cut.Find("[data-testid='tenants-detail-degraded']");
                cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("last-confirmed-user");
            }
            else
            {
                cut.Find("[data-testid='tenants-detail-identity']");
                MemberAccessReview memberReview = cut.FindComponent<MemberAccessReview>().Instance;
                memberReview.Members.Kind.ShouldBe(TenantUsersSurfaceKind.Degraded);
                memberReview.Members.Reason.ShouldBe(TenantUsersReason.GatewayFailure);
                cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("last-confirmed-user");
                cut.FindAll("[data-testid='tenants-member-refreshing']").ShouldBeEmpty();
            }
        });
    }

    [Theory]
    [InlineData("https%3A%2F%2Fexample.test%2Ftenants")]
    [InlineData("%2F%2Fevil.test%2Ftenants")]
    [InlineData("%2Fglobal-administrators")]
    public void Detail_page_rejects_unsafe_return_url(string encodedReturnUrl)
    {
        RegisterServices(_ => Task.FromResult(TenantDetailSnapshot.Ready(
            Detail("tenant.alpha"),
            "\"etag\"",
            ReadModelFreshnessState.Current).WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha"))));

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo($"/tenants/tenant.alpha?returnUrl={encodedReturnUrl}");

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");

        cut.Find("[data-testid='tenants-detail-back']").GetAttribute("href").ShouldBe("/tenants?auditReturnUnavailable=true");
    }

    [Theory]
    [InlineData(TenantDetailSurfaceKind.Stale, "tenants-detail-stale", "stale")]
    [InlineData(TenantDetailSurfaceKind.Degraded, "tenants-detail-degraded", "degraded")]
    [InlineData(TenantDetailSurfaceKind.Unauthorized, "tenants-detail-unauthorized", "authorized")]
    [InlineData(TenantDetailSurfaceKind.NotFound, "tenants-detail-error", "not found")]
    [InlineData(TenantDetailSurfaceKind.Unavailable, "tenants-detail-error", "unavailable")]
    [InlineData(TenantDetailSurfaceKind.Unknown, "tenants-detail-error", "unavailable")]
    public void Detail_page_renders_distinct_safe_states(
        TenantDetailSurfaceKind kind,
        string selector,
        string expectedText)
    {
        RegisterServices(_ => Task.FromResult(Snapshot(kind)));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement($"[data-testid='{selector}']");

        cut.Find($"[data-testid='{selector}']").TextContent.ShouldContain(expectedText, Case.Insensitive);
        cut.Find("#tenants-detail-identity").GetAttribute("tabindex").ShouldBe("-1");

        // The denial must be announced, not merely rendered. The only assertion covering this lived in the
        // Tier 3 route-smoke class, which carries [DaprFact] + SkipIfUnavailable() and runs
        // continue-on-error -- so a green report there was never proof it ran. It is asserted here, in a
        // blocking lane, as an element attribute rather than as an ordered raw-markup substring.
        if (kind is TenantDetailSurfaceKind.Unauthorized)
        {
            cut.Find($"[data-testid='{selector}']").GetAttribute("role").ShouldBe("alert");
        }

        if (kind is TenantDetailSurfaceKind.Unauthorized or TenantDetailSurfaceKind.NotFound or TenantDetailSurfaceKind.Unavailable or TenantDetailSurfaceKind.Unknown)
        {
            cut.Markup.ShouldNotContain("Tenant alpha description");
            cut.Markup.ShouldNotContain("tenants-config-table");
        }
    }

    [Fact]
    public void Detail_page_fails_closed_to_unavailable_when_degraded_snapshot_has_no_payload()
    {
        // A Degraded surface with a NULL detail payload (reachable via an unmapped gateway status ->
        // Degraded(null, ...) or a 304-not-modified with no cached detail) must NOT render the degraded
        // body. The FcAggregateDetailPage state mapping fails closed to Unavailable so a projection-less
        // state is never dressed as the ready surface. The payload-carrying Degraded case does not cover
        // this; this pins the fail-closed edge.
        RegisterServices(_ => Task.FromResult(
            TenantDetailSnapshot.Degraded(null, "Tenant detail query gateway returned a safe degraded state.").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha"))));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-error']");

        cut.Find("[data-testid='tenants-detail-error']").TextContent.ShouldContain("unavailable", Case.Insensitive);
        cut.FindAll("[data-testid='tenants-detail-degraded']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-detail-identity']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("tenants-config-table");
    }

    [Fact]
    public void Configuration_read_contract_accepts_only_the_safe_model_and_read_state()
    {
        Type component = typeof(TenantConfigurationView);

        component.GetProperty("Model").ShouldNotBeNull();
        component.GetProperty("Detail").ShouldBeNull();
        component.GetProperty("ProjectionEvidenceProvider").ShouldBeNull();
        component.GetProperty("RemoveProjectionEvidenceProvider").ShouldBeNull();
        component.GetProperty("OnCommandActivityChanged").ShouldBeNull();
    }

    [Theory]
    [InlineData(typeof(SetTenantConfigurationFlow))]
    [InlineData(typeof(RemoveTenantConfigurationFlow))]
    public void Configuration_management_flows_accept_only_safe_context_and_proof_contracts(Type component)
    {
        ArgumentNullException.ThrowIfNull(component);
        component.GetProperty("Context").ShouldNotBeNull();
        component.GetProperty("Detail").ShouldBeNull();
        component.GetProperty("ProjectionEvidenceProvider").ShouldNotBeNull();
        if (component == typeof(SetTenantConfigurationFlow))
        {
            component.GetProperty("ReauthorizeProvider").ShouldBeNull();
        }
        else
        {
            component.GetProperty("ReauthorizeProvider").ShouldBeNull();
            component.GetProperty("PreviewEvidenceProvider").ShouldNotBeNull();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Detail_page_set_configuration_requires_current_authorized_preview_before_dispatch(bool stillOwnerAtSubmit)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        TenantDetail ownerDetail = Detail(
            "tenant.alpha",
            new Dictionary<string, string> { ["billing.mode"] = "trial" },
            TenantStatus.Active,
            [new TenantMember("operator.alpha", TenantRole.TenantOwner)]);
        TenantDetail currentPreviewDetail = ownerDetail;
        int refusedPreviewCalls = 0;
        StubTenantCommandGateway gateway = new()
        {
            SetConfigurationSubmission = TenantCommandSubmissionResult.Accepted("ignored", "correlation-config"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 },
        };
        StubTenantsBffComposition composition = new();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        queryGateway.SupportsSetConfigurationPreview.Returns(true);
        queryGateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(ReadyWithSafeConfiguration(
                ownerDetail,
                projectionVersion: "tenant-sequence:41")));
        queryGateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(MemberSnapshot(ownerDetail)));
        queryGateway.GetSetConfigurationPreviewAsync(
                Arg.Any<TenantSetConfigurationIntent>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantSetConfigurationIntent intent = call.Arg<TenantSetConfigurationIntent>();
                if (!currentPreviewDetail.Members.Any(member => member.UserId == "operator.alpha"
                    && member.Role == TenantRole.TenantOwner))
                {
                    refusedPreviewCalls++;
                    return Task.FromResult(TenantSetConfigurationPreview.Unavailable(intent));
                }

                return Task.FromResult(TenantSetConfigurationPreview.Create(
                    intent,
                    TenantStatus.Active,
                    TenantSetConfigurationCurrentState.Different,
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current,
                    "tenant-sequence:41",
                    isAuthorized: true));
            });
        queryGateway.GetSetConfigurationProjectionProofAsync(
                Arg.Any<TenantSetConfigurationIntent>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantSetConfigurationIntent intent = call.Arg<TenantSetConfigurationIntent>();
                return Task.FromResult(TenantConfigurationProjectionProof.Create(
                    intent.TenantId,
                    TenantConfigurationProjectionProofKind.SetConfirmed,
                    "tenant-sequence:42",
                    intent.AttemptFingerprint));
            });
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(gateway);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddScoped<TenantSetConfigurationAttemptTracker>();
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-config-set-open']").Click();
        cut.Find("[data-testid='tenants-config-set-key-suffix']").Change("mode");
        cut.Find("[data-testid='tenants-config-set-value']").Change("production");
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-config-set-submit']")
            .GetAttribute("disabled").ShouldBeNull());

        if (!stillOwnerAtSubmit)
        {
            currentPreviewDetail = ownerDetail with
            {
                Members = [new TenantMember("owner-user", TenantRole.TenantOwner)],
            };
        }

        cut.Find("[data-testid='tenants-config-set-flow'] form").Submit();

        if (!stillOwnerAtSubmit)
        {
            cut.WaitForAssertion(() => cut.FindComponent<SetTenantConfigurationFlow>()
                .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
            refusedPreviewCalls.ShouldBeGreaterThan(0);
            gateway.SetConfigurationCallCount.ShouldBe(0);
            gateway.StatusCallCount.ShouldBe(0);
            _ = queryGateway.DidNotReceive().GetSetConfigurationProjectionProofAsync(
                Arg.Any<TenantSetConfigurationIntent>(),
                Arg.Any<CancellationToken>());
            return;
        }

        cut.WaitForAssertion(() => cut.FindComponent<SetTenantConfigurationFlow>()
            .Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        gateway.SetConfigurationCallCount.ShouldBe(1);
        gateway.LastMessageId.ShouldNotBeNullOrWhiteSpace();
        gateway.LastSetConfigurationRequest.ShouldNotBeNull().Key.ShouldBe("billing.mode");
        gateway.StatusCallCount.ShouldBe(1);
        gateway.LastStatusHandle.ShouldNotBeNull().AggregateId.ShouldBe("tenant.alpha");
        gateway.LastStatusHandle.MessageId.ShouldBe(gateway.LastMessageId);
        _ = queryGateway.Received().GetSetConfigurationPreviewAsync(
            Arg.Any<TenantSetConfigurationIntent>(),
            Arg.Any<CancellationToken>());
        _ = queryGateway.Received(1).GetSetConfigurationProjectionProofAsync(
            Arg.Any<TenantSetConfigurationIntent>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Detail_page_requires_fresh_preview_evidence_on_set_submit()
    {
        // The submit-time preview must be fresh. A render-time management grant cannot authorize
        // dispatch when current preview evidence is unavailable.
        JSInterop.Mode = JSRuntimeMode.Loose;
        StubTenantCommandGateway gateway = new()
        {
            SetConfigurationSubmission = TenantCommandSubmissionResult.Accepted("message-1", "correlation-config"),
            Status = new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 },
        };
        StubTenantsBffComposition composition = new();
        ITenantQueryGateway queryGateway = Substitute.For<ITenantQueryGateway>();
        queryGateway.SupportsSetConfigurationPreview.Returns(true);
        queryGateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }))));
        queryGateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(MemberSnapshot(Detail("tenant.alpha"))));
        queryGateway.GetSetConfigurationPreviewAsync(
                Arg.Any<TenantSetConfigurationIntent>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantSetConfigurationPreview.Unavailable(
                call.Arg<TenantSetConfigurationIntent>())));
        Services.AddSingleton(queryGateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(gateway);
        Services.AddSingleton<ITenantsBffComposition>(composition);
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-config-set-open']");

        cut.Find("[data-testid='tenants-config-set-open']").Click();
        cut.Find("[data-testid='tenants-config-set-key-suffix']").Change("mode");
        cut.Find("[data-testid='tenants-config-set-value']").Change("production");
        // Scope to the set-flow form: the detail page hosts other forms, and clicking the Submit button
        // is a no-op while IsSubmitDisabled is true (preview not yet complete in some bUnit sequences).
        // EditForm.Submit() invokes OnSubmit directly, matching the standalone flow tests.
        cut.Find("[data-testid='tenants-config-set-flow'] form").Submit();

        _ = queryGateway.Received().GetSetConfigurationPreviewAsync(
            Arg.Any<TenantSetConfigurationIntent>(),
            Arg.Any<CancellationToken>());
        gateway.SetConfigurationCallCount.ShouldBe(0);
    }

    [Fact]
    public void Detail_page_configuration_summary_renders_unavailable_when_policy_cannot_be_verified()
    {
        // Deleting the `!configuration.IsAvailable` branch would render the absence claim for unverifiable
        // policy. The sibling read landmark already pins this distinction; the page summary must too.
        TenantDetail detail = Detail("tenant.alpha");
        TenantConfigurationComposition unavailableComposition = new(
            TenantConfigurationSafeComposer.SanitizeDetail(detail),
            TenantConfigurationSafeModel.Unavailable(detail.TenantId),
            TenantConfigurationManagementContext.Unavailable(detail.TenantId, detail.Status));
        RegisterServices(_ => Task.FromResult(TenantDetailSnapshot.Ready(
            unavailableComposition,
            "\"etag\"",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            "projection-v1").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha"))));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-configuration-summary']");

        string summary = cut.Find("[data-testid='tenants-detail-configuration-summary']").TextContent;
        summary.ShouldContain("Configuration unavailable");
        summary.ShouldNotContain("No visible configuration");
    }

    [Fact]
    public void Detail_page_configuration_summary_renders_valid_empty_without_unavailable_copy()
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha", new Dictionary<string, string>()))));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-configuration-summary']");

        string summary = cut.Find("[data-testid='tenants-detail-configuration-summary']").TextContent;
        summary.ShouldContain("No visible configuration");
        summary.ShouldNotContain("Configuration unavailable");
    }

    [Fact]
    public void Configuration_read_view_renders_only_positive_safe_rows_without_mutation_or_copy_affordances()
    {
        RegisterComponentServices();
        TenantConfigurationSafeModel model = SafeConfiguration(
            ("billing", "billing.mode", "trial"),
            ("feature", "feature", "enabled"));

        IRenderedComponent<TenantConfigurationView> cut = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, model)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current));

        cut.FindAll("[data-testid='tenants-config-read-group']").Count.ShouldBe(2);
        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain("billing.mode");
        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain("trial");
        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain("feature");
        // aria-label is prohibited on role=code, so the literal must reach AT as text content instead.
        cut.Find("[data-testid='tenants-config-read-key']").TextContent.ShouldBe("billing.mode");
        cut.Find("[data-testid='tenants-config-read-value']").TextContent.ShouldBe("trial");
        cut.Find("[data-testid='tenants-config-read-key']").GetAttribute("aria-label").ShouldBeNull();
        cut.Find("[data-testid='tenants-config-read-value']").GetAttribute("aria-label").ShouldBeNull();
        cut.Markup.ShouldNotContain("tenants-config-set-flow");
        cut.Markup.ShouldNotContain("tenants-config-remove");
        cut.Markup.ShouldNotContain("<form", Case.Insensitive);
        cut.FindAll("[data-copy-kind='ConfigurationKey']").ShouldBeEmpty();
        cut.FindAll("[data-copy-kind='SafeConfigurationValue']").ShouldBeEmpty();
    }

    [Fact]
    public void Configuration_read_view_keeps_valid_empty_policy_unavailable_and_filtered_empty_distinct()
    {
        RegisterComponentServices();
        IRenderedComponent<TenantConfigurationView> unavailable = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, TenantConfigurationSafeModel.Unavailable("tenant.alpha"))
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current));

        unavailable.Find("[data-testid='tenants-config-read-state']").TextContent.ShouldContain("unavailable", Case.Insensitive);
        unavailable.Markup.ShouldNotContain("tenants-config-read-empty");
        unavailable.Markup.ShouldNotContain("visible configuration entries", Case.Insensitive);

        IRenderedComponent<TenantConfigurationView> empty = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, SafeConfiguration())
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current));

        empty.Find("[data-testid='tenants-config-read-empty']").TextContent.ShouldContain("No visible configuration");
        empty.Find("[data-testid='tenants-config-read-empty']").GetAttribute("aria-describedby")
            .ShouldBe("tenants-config-read-announcer");
        empty.Find("[data-testid='tenants-config-read-empty']").HasAttribute("role").ShouldBeFalse();
        empty.Markup.ShouldNotContain("tenants-config-read-filtered-empty");

        IRenderedComponent<TenantConfigurationView> filtered = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, SafeConfiguration(("billing", "billing.mode", "trial")))
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current));

        filtered.Find("[data-testid='tenants-config-read-filter']").Change("missing");

        filtered.Find("[data-testid='tenants-config-read-filtered-empty']").TextContent.ShouldContain("No visible configuration matches");
        filtered.Find("[data-testid='tenants-config-read-announcer']").TextContent.ShouldContain("0 visible configuration entries");

        // Both empty panels are described by the announcer, and neither carries a role of its own on that
        // basis. Nothing asserted the association, so deleting either attribute left the suite green while
        // the panels' own justification for having no role silently stopped being true. The announcer's id
        // is asserted too: aria-describedby pointing at an element that does not exist is the same defect
        // one level down.
        filtered.Find("[data-testid='tenants-config-read-announcer']").GetAttribute("id")
            .ShouldBe("tenants-config-read-announcer");
        filtered.Find("[data-testid='tenants-config-read-filtered-empty']").GetAttribute("aria-describedby")
            .ShouldBe("tenants-config-read-announcer");
        filtered.Find("[data-testid='tenants-config-read-filtered-empty']").HasAttribute("role").ShouldBeFalse();
        filtered.Find("[data-testid='tenants-config-read-clear-filter']").Click();
        filtered.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain("billing.mode");
    }

    [Fact]
    public void Configuration_read_view_preserves_literal_namespace_unicode_accessibility_and_responsive_overflow()
    {
        RegisterComponentServices();
        IRenderedComponent<TenantConfigurationView> cut = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, SafeConfiguration(
                ("billing..Α", "billing..Α.<script>\u202E", "値🙂<literal>"),
                ("identity", "identity.region", "eu")))
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-config-read-filter']").Change("billing");

        cut.FindAll("[data-testid='tenants-config-read-group']").Count.ShouldBe(1);
        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain("Namespace billing..Α");
        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain("billing..Α.<script>\u202E");
        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain("値🙂<literal>");
        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldNotContain("identity.region");
        cut.Find("[data-testid='tenants-config-read-truth-state']").TextContent.ShouldContain("Current");
        cut.Find(".tenant-config__scope").TextContent.ShouldContain("approved by the current namespace and display policy");
        cut.Find("[data-testid='tenants-config-read-announcer']").TextContent.ShouldContain("1 visible configuration entries");

        // Routine result counts stay polite; assertive belongs to the truth state, which owns its own region.
        cut.Find("[data-testid='tenants-config-read-announcer']").GetAttribute("aria-live").ShouldBe("polite");
        cut.Find("[data-testid='tenants-config-read-filter']").GetAttribute("aria-describedby").ShouldBe("tenants-config-read-filter-help");
        cut.Find("[data-testid='tenants-config-read-clear-filter']").NodeName.ShouldBe("FLUENT-BUTTON");
        cut.Find("[data-testid='tenants-config-read-table']").GetAttribute("tabindex").ShouldBe("0");
        cut.Find("[data-testid='tenants-config-read-table']").GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
        // Overflow contract is owned by Class="tenant-config__table-wrap" on the live FluentStack host.
        cut.Find("[data-testid='tenants-config-read-table']").GetAttribute("class")!
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .ShouldContain("tenant-config__table-wrap");
    }

    [Theory]
    // data-state is the text-independent discriminator the production comment promises, so the expected
    // value is named per row: asserting only ShouldNotBeNullOrWhiteSpace passed for any constant, including
    // a hardcoded one, because the value is a substring of a prefix that is never blank.
    //
    // role and aria-live are asserted together. role="status" carries an implicit polite live region, so an
    // assertive escalation has to switch the role to "alert" or the two contradict each other; asserting a
    // fixed "status" pinned the contradiction rather than the behaviour.
    [InlineData(TenantDetailSurfaceKind.Stale, ReadModelFreshnessState.Stale, "stale", "Stale", "status", "polite")]
    [InlineData(TenantDetailSurfaceKind.Degraded, ReadModelFreshnessState.Unknown, "degraded", "Degraded", "alert", "assertive")]
    [InlineData(TenantDetailSurfaceKind.Unknown, ReadModelFreshnessState.Unknown, "Unknown", "Unknown", "alert", "assertive")]
    public void Configuration_read_view_surfaces_non_current_truth_without_collapsing_to_success(
        TenantDetailSurfaceKind kind,
        ReadModelFreshnessState freshness,
        string expectedText,
        string expectedStateKind,
        string expectedRole,
        string expectedPoliteness)
    {
        RegisterComponentServices();

        IRenderedComponent<TenantConfigurationView> cut = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, SafeConfiguration(("billing", "billing.mode", "trial")))
            .Add(view => view.SurfaceKind, kind)
            .Add(view => view.Freshness, freshness)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current));

        // The truth sentence has one owner: the state section, which is also the only live region for it.
        // It used to be duplicated into the header badge row and was never announced from there.
        cut.Find("[data-testid='tenants-config-read-state']").TextContent.ShouldContain(expectedText, Case.Insensitive);
        cut.Find("[data-testid='tenants-config-read-state']").GetAttribute("data-state").ShouldBe(expectedStateKind);
        cut.Find("[data-testid='tenants-config-read-state']").GetAttribute("role").ShouldBe(expectedRole);
        cut.Find("[data-testid='tenants-config-read-state']").GetAttribute("aria-live").ShouldBe(expectedPoliteness);

        // Asserted over rendered text, not raw markup. ShouldNotContain is case-insensitive, so scanning the
        // markup also scans class tokens and attribute names -- the Current lifecycle badge carries a
        // success-toned class -- which is the incidental-markup coupling the project rules forbid. What must
        // never appear is a success claim the operator can read.
        cut.Find("section.tenant-config").TextContent.ShouldNotContain("Success", Case.Insensitive);
    }

    [Theory]
    // The lifecycle read state had no test at all: deleting `|| HasNonCurrentLifecycle` from HasNonCurrentState
    // and collapsing the ProjectionLifecycle arm to Ready both survived the whole suite. The reachable shape
    // is Freshness=Current with Lifecycle=Unknown -- a response with no lifecycle header on the wire.
    //
    // The copy is asserted as a whole localized string, not a substring: the title used to claim "Projection
    // rebuilding" for all six non-Current lifecycles, including Unknown, which asserts a rebuild that is not
    // happening over a surface whose real problem is that it has no lifecycle evidence.
    [InlineData(ProjectionLifecycleState.Unknown)]
    [InlineData(ProjectionLifecycleState.Rebuilding)]
    [InlineData(ProjectionLifecycleState.Degraded)]
    [InlineData(ProjectionLifecycleState.Unavailable)]
    [InlineData(ProjectionLifecycleState.LocalOnly)]
    public void Configuration_read_view_reports_a_non_current_projection_lifecycle_without_claiming_a_rebuild(
        ProjectionLifecycleState lifecycle)
    {
        RegisterComponentServices();

        IRenderedComponent<TenantConfigurationView> cut = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, SafeConfiguration(("billing", "billing.mode", "trial")))
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, lifecycle));

        IElement state = cut.Find("[data-testid='tenants-config-read-state']");
        state.GetAttribute("data-state").ShouldBe("ProjectionLifecycle");
        state.TextContent.ShouldContain("Projection not current");
        state.TextContent.ShouldContain("cannot be treated as authoritative");
        state.TextContent.ShouldNotContain("rebuilding", Case.Insensitive);
        state.TextContent.ShouldNotContain("evidence is current", Case.Insensitive);

        // An Unavailable lifecycle is the one that escalates the live region, and role has to follow it or
        // role="status"'s implicit polite live region contradicts the assertive attribute. Replacing
        // LivePoliteness with the literal "polite" made this escalation disappear silently: the only
        // aria-live assertion in this file was on the announcer, a different element.
        bool expectAssertive = lifecycle is ProjectionLifecycleState.Unavailable;
        state.GetAttribute("aria-live").ShouldBe(expectAssertive ? "assertive" : "polite");
        state.GetAttribute("role").ShouldBe(expectAssertive ? "alert" : "status");
    }

    /// <summary>
    /// Accordion groups are keyed by namespace, so a reorder moves the group, not its expanded state.
    /// </summary>
    /// <remarks>
    /// Without <c>@key</c> Blazor reuses accordion items positionally, so the component's own DOM-held
    /// collapsed state follows position instead of namespace: collapse "billing", let the projection return
    /// the namespaces in a different order, and "identity" is now the collapsed one. The mutation that
    /// deletes the key survived because no test drove expansion across two renders.
    /// </remarks>
    [Fact]
    public void Configuration_group_identity_follows_the_namespace_across_a_reorder()
    {
        RegisterComponentServices();
        IRenderedComponent<TenantConfigurationView> cut = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, SafeConfiguration(
                ("billing", "billing.mode", "trial"),
                ("identity", "identity.region", "eu")))
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current));

        IReadOnlyList<IElement> initial = cut.FindAll("fluent-accordion-item");
        initial.Count.ShouldBe(2);
        string?[] initialKeys = [.. initial.Select(static item => item.GetAttribute("id"))];

        // Same namespaces, opposite order: what identifies a group is its namespace, not its position.
        cut.Render(parameters => parameters
            .Add(view => view.Model, SafeConfiguration(
                ("identity", "identity.region", "eu"),
                ("billing", "billing.mode", "trial")))
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current));

        IReadOnlyList<IElement> reordered = cut.FindAll("fluent-accordion-item");
        reordered.Count.ShouldBe(2);

        // Keyed rendering moves the existing element with its namespace, so the identity assigned to the
        // "billing" group in the first render is the one still attached to it after the reorder. Positional
        // reuse would instead leave the first slot's identity in place while its content changed.
        string?[] reorderedKeys = [.. reordered.Select(static item => item.GetAttribute("id"))];
        reorderedKeys.ShouldBe(initialKeys.Reverse().ToArray());

        // ...and the rows travel with their group rather than being re-bound onto the other one.
        reordered[0].TextContent.ShouldContain("identity.region");
        reordered[1].TextContent.ShouldContain("billing.mode");
    }

    [Fact]
    public void Configuration_read_view_reports_ready_only_when_lifecycle_and_freshness_are_both_current()
    {
        RegisterComponentServices();

        IRenderedComponent<TenantConfigurationView> cut = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, SafeConfiguration(("billing", "billing.mode", "trial")))
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current));

        cut.FindAll("[data-testid='tenants-config-read-state']").ShouldBeEmpty();
    }

    [Fact]
    public async Task Management_keeps_an_open_remove_flow_mounted_after_its_target_row_leaves_the_context()
    {
        // The refresh that proves a removal drops the row from RemovableRows. Unmounting the flow on that
        // signal destroyed the Confirmed state, the audit entry point and the recovery text at the exact
        // moment the projection proved the removal, and orphaned focus on the destroyed subtree.
        RegisterComponentServices();
        TenantConfigurationSafeRow row = new("billing", "billing.mode", "trial");
        TenantConfigurationManagementContext withRow = TenantConfigurationManagementContext.Available(
            "tenant.alpha", TenantStatus.Active, false, ["billing"], [row],
            TenantConfigurationAuthorityState.TenantOwner);

        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, withRow)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.SetEvidence, ManagementEvidence(withRow, TenantHighImpactAction.SetConfiguration))
            .Add(p => p.RemoveEvidence, ManagementEvidence(withRow, TenantHighImpactAction.RemoveConfiguration)));

        cut.Find("[data-testid='tenants-config-management-remove-open']").Click();
        cut.Find("[data-testid='tenants-config-remove-flow']");

        // The flow reports its command activity before the parent refresh lands.
        IRenderedComponent<RemoveTenantConfigurationFlow> flow = cut.FindComponent<RemoveTenantConfigurationFlow>();
        await cut.InvokeAsync(() => flow.Instance.CommandActivityLease!(true));

        TenantConfigurationManagementContext withoutRow = TenantConfigurationManagementContext.Available(
            "tenant.alpha", TenantStatus.Active, false, ["billing"], [],
            TenantConfigurationAuthorityState.TenantOwner);
        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, withoutRow)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.FindAll("[data-testid='tenants-config-remove-flow']").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Management_keeps_both_flows_mounted_when_the_command_surface_goes_unavailable_under_its_own_command()
    {
        // The command surface goes unavailable *because* a flow submitted: the flow raises activity, the
        // detail page sets _commandInFlight, and IsCommandSurfaceAvailable comes straight back down here.
        // Without the in-flight exception on the CommandSurface clause, this branch replaced both flows with
        // a static paragraph at the moment of submit, so Confirmed, Rejected, UnableToVerify, the safe
        // message and the audit entry point were never renderable for a configuration command -- the
        // non-collapse guarantee, defeated one level above where both flows already implement it.
        RegisterComponentServices();
        TenantConfigurationSafeRow row = new("billing", "billing.mode", "trial");
        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            "tenant.alpha", TenantStatus.Active, false, ["billing"], [row],
            TenantConfigurationAuthorityState.TenantOwner);

        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsCommandSurfaceAvailable, true)
            .Add(p => p.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(p => p.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration)));

        cut.Find("[data-testid='tenants-config-management-remove-open']").Click();
        IRenderedComponent<RemoveTenantConfigurationFlow> flow = cut.FindComponent<RemoveTenantConfigurationFlow>();
        await cut.InvokeAsync(() => flow.Instance.CommandActivityLease!(true));

        // The page reflects the in-flight command back as an unavailable command surface.
        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsCommandSurfaceAvailable, false));

        cut.FindAll("[data-testid='tenants-config-remove-flow']").ShouldNotBeEmpty();
        cut.FindAll("[data-testid='tenants-config-management-unavailable']").ShouldBeEmpty();

        // Once the command settles, the ordinary unavailable rule applies again.
        await cut.InvokeAsync(() => flow.Instance.CommandActivityLease!(false));
        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsCommandSurfaceAvailable, false));

        cut.FindAll("[data-testid='tenants-config-remove-flow']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-config-management-unavailable']").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Dismissing_remove_does_not_release_a_sibling_set_command_lease()
    {
        RegisterComponentServices();
        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            "tenant.alpha",
            TenantStatus.Active,
            false,
            ["billing"],
            [new TenantConfigurationSafeRow("billing", "billing.mode", "trial")],
            TenantConfigurationAuthorityState.TenantOwner);

        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsCommandSurfaceAvailable, true)
            .Add(p => p.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(p => p.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration)));

        IRenderedComponent<SetTenantConfigurationFlow> setFlow = cut.FindComponent<SetTenantConfigurationFlow>();
        await cut.InvokeAsync(() => setFlow.Instance.CommandActivityLease!(true));
        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsCommandSurfaceAvailable, false));

        cut.Find("[data-testid='tenants-config-management-remove-open']").Click();
        IRenderedComponent<RemoveTenantConfigurationFlow> removeFlow = cut.FindComponent<RemoveTenantConfigurationFlow>();
        await cut.InvokeAsync(() => removeFlow.Instance.OnCloseRequested.InvokeAsync());

        cut.FindAll("[data-testid='tenants-config-remove-flow']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-config-set-flow']").ShouldNotBeEmpty();
        cut.FindAll("[data-testid='tenants-config-management-unavailable']").ShouldBeEmpty();

        setFlow = cut.FindComponent<SetTenantConfigurationFlow>();
        await cut.InvokeAsync(() => setFlow.Instance.CommandActivityLease!(false));
        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsCommandSurfaceAvailable, false));
        cut.FindAll("[data-testid='tenants-config-management-unavailable']").ShouldNotBeEmpty();
    }

    [Fact]
    public async Task Policy_loss_keeps_a_retained_set_flow_mounted_for_reconciliation()
    {
        RegisterComponentServices();
        TenantConfigurationManagementContext available = TenantConfigurationManagementContext.Available(
            "tenant.alpha",
            TenantStatus.Active,
            false,
            ["billing"],
            [new TenantConfigurationSafeRow("billing", "billing.mode", "trial")],
            TenantConfigurationAuthorityState.TenantOwner);
        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, available)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.IsCommandSurfaceAvailable, true)
            .Add(p => p.SetEvidence, ManagementEvidence(available, TenantHighImpactAction.SetConfiguration))
            .Add(p => p.RemoveEvidence, ManagementEvidence(available, TenantHighImpactAction.RemoveConfiguration)));
        SetTenantConfigurationFlow setFlow = cut.FindComponent<SetTenantConfigurationFlow>().Instance;
        await cut.InvokeAsync(() => setFlow.CommandActivityLease!(true));

        TenantConfigurationManagementContext unavailable = TenantConfigurationManagementContext.Unavailable(
            "tenant.alpha",
            TenantStatus.Active);
        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, unavailable)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Unauthorized)
            .Add(p => p.Freshness, ReadModelFreshnessState.Unknown)
            .Add(p => p.IsCommandSurfaceAvailable, false));

        cut.FindAll("[data-testid='tenants-config-set-flow']").ShouldNotBeEmpty();
        cut.FindAll("[data-testid='tenants-config-management-targets']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-config-remove-flow']").ShouldBeEmpty();
    }

    [Fact]
    public void Management_associates_its_unavailable_reason_with_the_landmark_it_disables()
    {
        // An inline reason has to be programmatically associated, not merely adjacent. The paragraph carried
        // an id that nothing referenced, so assistive technology never connected it to the region whose
        // controls it explains -- and deleting the binding survived the whole suite.
        RegisterComponentServices();
        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, TenantConfigurationManagementContext.Available(
                "tenant.alpha", TenantStatus.Active, false, ["billing"],
                [new TenantConfigurationSafeRow("billing", "billing.mode", "trial")],
                TenantConfigurationAuthorityState.TenantOwner))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Stale)
            .Add(p => p.Freshness, ReadModelFreshnessState.Stale));

        string reasonId = cut.Find("[data-testid='tenants-config-management-unavailable']").Id.ShouldNotBeNull();
        cut.Find("[data-testid='tenants-config-management-section']")
            .GetAttribute("aria-describedby").ShouldBe(reasonId);
    }

    [Fact]
    public void Management_drops_the_association_when_nothing_is_disabled()
    {
        RegisterComponentServices();
        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, TenantConfigurationManagementContext.Available(
                "tenant.alpha", TenantStatus.Active, false, ["billing"],
                [new TenantConfigurationSafeRow("billing", "billing.mode", "trial")],
                TenantConfigurationAuthorityState.TenantOwner))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.FindAll("[data-testid='tenants-config-management-unavailable']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-config-management-section']")
            .GetAttribute("aria-describedby").ShouldBeNull();
    }

    [Fact]
    public async Task Management_moves_focus_to_the_landmark_heading_when_the_row_it_came_from_is_gone()
    {
        // A successful removal deletes the row whose launch control focus would return to. The fallback was
        // armed only in OnParametersSet, but CloseRemoveFlowAsync runs as an EventCallback from the child --
        // which re-renders this component without running SetParametersAsync -- so on the one path that
        // needs it, neither branch of OnAfterRenderAsync ran and focus fell to <body>.
        //
        // Focus goes through JS interop, so the discriminator is that a focus call is made at all on this
        // path: with the fallback neutered, none is.
        RegisterComponentServices();
        JSInterop.Mode = JSRuntimeMode.Loose;
        TenantConfigurationSafeRow row = new("billing", "billing.mode", "trial");
        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            "tenant.alpha", TenantStatus.Active, false, ["billing"], [row],
            TenantConfigurationAuthorityState.TenantOwner);

        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(p => p.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration)));

        cut.Find("[data-testid='tenants-config-management-remove-open']").Click();
        IRenderedComponent<RemoveTenantConfigurationFlow> flow = cut.FindComponent<RemoveTenantConfigurationFlow>();
        await cut.InvokeAsync(() => flow.Instance.CommandActivityLease!(true));

        // The projection proves the removal: the row leaves, and with it the launch control focus would
        // otherwise return to.
        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, TenantConfigurationManagementContext.Available(
                "tenant.alpha", TenantStatus.Active, false, ["billing"], [],
                TenantConfigurationAuthorityState.TenantOwner))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        int focusCallsBeforeClose = FocusedElementIds().Count;

        await cut.InvokeAsync(() => flow.Instance.OnCloseRequested.InvokeAsync());

        // WHICH element received focus, not merely that a focus call happened. ElementReference.FocusAsync
        // routes every element through the same interop identifier, and this component has two focus paths
        // (_focusRemoveLaunchKey -> the row's launch span, _focusHeadingPending -> the landmark heading), so
        // counting calls could not tell them apart -- nor could it tell either of them from a dialog's own
        // focus sentinels. bUnit renders the element-reference marker without its id, so the target cannot be
        // correlated through markup; the component's captured reference is read directly instead.
        IReadOnlyList<string> focusedAfterClose = FocusedElementIds();
        focusedAfterClose.Count.ShouldBe(focusCallsBeforeClose + 1);
        focusedAfterClose[^1].ShouldBe(CapturedElementReferenceId(cut.Instance, "_headingElement"));

        // The fallback target has to be programmatically focusable.
        cut.Find("#tenants-config-management-heading").GetAttribute("tabindex").ShouldBe("-1");

        // A later projection render must not replay the one-shot fallback and steal focus from whatever the
        // operator moved to after dismissal.
        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, TenantConfigurationManagementContext.Available(
                "tenant.alpha", TenantStatus.Active, false, ["billing"], [],
                TenantConfigurationAuthorityState.TenantOwner))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));
        FocusedElementIds().Count.ShouldBe(focusedAfterClose.Count);
    }

    [Fact]
    public void Management_reports_a_failed_read_rather_than_the_projection_lifecycle()
    {
        // Decision D-F clause ordering at the landmark: an Unavailable surface also carries a non-Current
        // lifecycle, so with the lifecycle clause first the operator was told to refresh the projection
        // lifecycle when the read had simply failed.
        RegisterComponentServices();
        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Unavailable)
            .Add(p => p.Context, TenantConfigurationManagementContext.Available(
                "tenant.alpha", TenantStatus.Active, false, ["billing"],
                [new TenantConfigurationSafeRow("billing", "billing.mode", "trial")],
                TenantConfigurationAuthorityState.TenantOwner))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Unavailable)
            .Add(p => p.Freshness, ReadModelFreshnessState.Unknown));

        string reason = cut.Find("[data-testid='tenants-config-management-unavailable']").TextContent.Trim();
        reason.ShouldBe("Refresh available tenant detail before managing configuration.");
    }

    /// <summary>
    /// The metadata flow reports a failed read rather than the projection lifecycle.
    /// </summary>
    /// <remarks>
    /// Decision D-F applies at five gate sites and requires a test per site pinning the reason string for a
    /// failed-read snapshot. This was the unguarded one: restoring the pre-D-F clause order here left the
    /// suite green, while the identical mutation on <c>TenantConfigurationManagement</c> was killed by the
    /// sibling above. Every failed-read snapshot also carries a non-Current lifecycle, so with the lifecycle
    /// clause first an operator whose read had simply failed was told to refresh the projection lifecycle.
    /// </remarks>
    [Fact]
    public void Metadata_flow_reports_a_failed_read_rather_than_the_projection_lifecycle()
    {
        RegisterComponentServices();
        IRenderedComponent<EditTenantMetadataFlow> cut = Render<EditTenantMetadataFlow>(parameters => parameters
            .Add(p => p.IsAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.Detail, Detail("tenant.alpha"))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Unavailable)
            .Add(p => p.Freshness, ReadModelFreshnessState.Unknown)
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Unavailable));

        string reason = cut.Find("[data-testid='tenants-edit-metadata-unavailable-reason']").TextContent.Trim();
        reason.ShouldBe("Refresh current tenant detail before editing metadata.");
    }

    [Fact]
    public void Detail_page_passes_projection_version_into_edit_metadata_flow()
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"),
            ProjectionLifecycleState.Current,
            "projection-v1")));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-edit-metadata-flow']");

        EditTenantMetadataFlow metadataFlow = cut.FindComponent<EditTenantMetadataFlow>().Instance;
        metadataFlow.ProjectionVersion.ShouldBe("projection-v1");
        Func<string?> provider = metadataFlow.ProjectionVersionProvider.ShouldNotBeNull();
        provider().ShouldBe("projection-v1");
    }

    [Fact]
    public async Task Detail_page_confirms_metadata_update_from_the_authoritative_proof_read()
    {
        // End-to-end page handshake. The flow-level tests supply their own version/evidence providers, and
        // the composition test only reads the provider once against a static snapshot -- so nothing pinned
        // that the PAGE actually produces post-refresh evidence at the moment ConfirmProjection runs. A
        // regression in the page half would otherwise turn every real edit into UnableToVerify while the
        // suite stayed green.
        JSInterop.Mode = JSRuntimeMode.Loose;
        TenantDetail original = Detail("tenant.alpha");
        TenantDetail renamed = original with { Name = "Alpha renamed" };
        TaskCompletionSource<TenantDetailSnapshot> refreshRead = new(TaskCreationOptions.RunContinuationsAsynchronously);

        int detailReads = 0;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                detailReads++;
                return detailReads <= 3
                    ? Task.FromResult(ReadyWithSafeConfiguration(original, ProjectionLifecycleState.Current, "projection-v1"))
                    : refreshRead.Task;
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>().TenantId))));

        // The authoritative proof read is what the page must consult for confirmation evidence.
        gateway.GetUpdateMetadataProjectionProofAsync(Arg.Any<UpdateTenant>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(
                ReadyWithSafeConfiguration(renamed, ProjectionLifecycleState.Current, "projection-v2")));
        ConfigureAuthoritativeAuditCapability(gateway);

        ITenantCommandGateway commandGateway = Substitute.For<ITenantCommandGateway>();
        commandGateway.UpdateTenantAsync(Arg.Any<UpdateTenant>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandSubmissionResult.Accepted("message-1", "correlation-1")));
        commandGateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 }));

        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-edit-metadata-flow']");

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Alpha renamed");
        Task submit = cut.Find("form").SubmitAsync();

        EditTenantMetadataFlow metadataFlow = cut.FindComponent<EditTenantMetadataFlow>().Instance;
        cut.WaitForAssertion(() =>
        {
            // Initial detail plus fresh pre-admission and post-lease authority reads precede the held refresh.
            detailReads.ShouldBe(4);
            metadataFlow.Snapshot.State.ShouldBe(TenantCommandLifecycleState.ProjectionPending);
            // The page has genuinely rerendered during the suspended refresh and lowered admission for
            // sibling commands. The metadata flow still owns that lease and must remain eligible to settle.
            metadataFlow.IsCommandSurfaceAvailable.ShouldBeFalse();
        });

        refreshRead.SetResult(ReadyWithSafeConfiguration(
            renamed,
            ProjectionLifecycleState.Current,
            "projection-v2"));
        // Observe the complete submit/status/proof handshake after releasing the suspended read.
        await submit.WaitAsync(TimeSpan.FromSeconds(5));

        cut.WaitForAssertion(() => metadataFlow.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        metadataFlow.Snapshot.LastConfirmedName.ShouldBe("Alpha renamed");
        // Confirmed without an attempt-matched Ready receipt: proof is unsupported in-panel, never pending forever.
        metadataFlow.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.MissingSupport);
        metadataFlow.IsCommandSurfaceAvailable.ShouldBeTrue();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        _ = gateway.DidNotReceive().GetTenantAuditAsync(
            Arg.Is<TenantAuditRequest>(request => request.From.HasValue),
            Arg.Any<TenantAuditSnapshot?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Detail_page_confirms_identical_metadata_from_the_exact_authoritative_audit_row()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        TenantDetail original = Detail("tenant.alpha");

        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                ReadyWithSafeConfiguration(original, ProjectionLifecycleState.Current, "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>().TenantId))));
        gateway.GetUpdateMetadataProjectionProofAsync(Arg.Any<UpdateTenant>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                ReadyWithSafeConfiguration(original, ProjectionLifecycleState.Current, "projection-v2")));
        ConfigureAuthoritativeAuditCapability(gateway);
        ConfigureMetadataAuditProof(gateway, request => MetadataAuditSnapshot(
            request,
            MetadataAuditRow(
                "message-1",
                request.TenantId,
                request.From.ShouldNotBeNull())));

        ITenantCommandGateway commandGateway = Substitute.For<ITenantCommandGateway>();
        commandGateway.UpdateTenantAsync(Arg.Any<UpdateTenant>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandSubmissionResult.Accepted("message-1", "correlation-1")));
        commandGateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 }));

        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-edit-metadata-flow']");

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("form").Submit();

        EditTenantMetadataFlow metadataFlow = cut.FindComponent<EditTenantMetadataFlow>().Instance;
        cut.WaitForAssertion(() => metadataFlow.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        metadataFlow.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.AlreadyApplied);
        metadataFlow.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable);
        metadataFlow.IsCommandSurfaceAvailable.ShouldBeTrue();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        _ = gateway.Received(1).GetTenantAuditAsync(
            Arg.Is<TenantAuditRequest>(request =>
                request.TenantId == "tenant.alpha"
                && request.From.HasValue
                && request.Category == AuditEventCategory.Administrative
                && request.PageSize == 100),
            Arg.Any<TenantAuditSnapshot?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Detail_page_confirms_identical_metadata_from_a_qualifying_row_on_a_later_audit_page()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        TenantDetail original = Detail("tenant.alpha");

        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                ReadyWithSafeConfiguration(original, ProjectionLifecycleState.Current, "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>().TenantId))));
        gateway.GetUpdateMetadataProjectionProofAsync(Arg.Any<UpdateTenant>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                ReadyWithSafeConfiguration(original, ProjectionLifecycleState.Current, "projection-v2")));
        ConfigureAuthoritativeAuditCapability(gateway);
        ConfigureMetadataAuditProof(gateway, request =>
        {
            TenantAuditRow unrelatedRow = MetadataAuditRow(
                "message-unrelated",
                request.TenantId,
                request.From.ShouldNotBeNull());
            return string.IsNullOrWhiteSpace(request.Cursor)
                ? MetadataAuditSnapshot(request, unrelatedRow, hasMore: true)
                : MetadataAuditSnapshot(
                    request,
                    MetadataAuditRow("message-1", request.TenantId, request.From.ShouldNotBeNull()));
        });

        ITenantCommandGateway commandGateway = Substitute.For<ITenantCommandGateway>();
        commandGateway.UpdateTenantAsync(Arg.Any<UpdateTenant>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandSubmissionResult.Accepted("message-1", "correlation-1")));
        commandGateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 }));

        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-edit-metadata-flow']");

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("form").Submit();

        EditTenantMetadataFlow metadataFlow = cut.FindComponent<EditTenantMetadataFlow>().Instance;
        cut.WaitForAssertion(() => metadataFlow.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed));
        metadataFlow.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditAvailable);
        _ = gateway.Received(1).GetTenantAuditAsync(
            Arg.Is<TenantAuditRequest>(request => request.PageSize == 100 && string.IsNullOrWhiteSpace(request.Cursor)),
            Arg.Any<TenantAuditSnapshot?>(),
            Arg.Any<CancellationToken>());
        _ = gateway.Received(1).GetTenantAuditAsync(
            Arg.Is<TenantAuditRequest>(request => request.PageSize == 100 && request.Cursor == "opaque-next-page"),
            Arg.Any<TenantAuditSnapshot?>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Route_change_discards_a_delayed_metadata_detail_proof_and_resets_the_flow_for_the_new_tenant()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        TenantDetail alpha = Detail("tenant.alpha");
        TenantDetail beta = Detail("tenant.beta") with
        {
            Name = "Beta",
            Description = "Tenant beta description",
        };
        var proofStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedProof = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        CancellationToken proofCancellation = default;

        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string tenantId = call.Arg<TenantDetailRequest>().TenantId;
                TenantDetail detail = tenantId == beta.TenantId ? beta : alpha;
                string projectionVersion = tenantId == beta.TenantId ? "beta-v1" : "alpha-v1";
                return Task.FromResult(ReadyWithSafeConfiguration(
                    detail,
                    ProjectionLifecycleState.Current,
                    projectionVersion));
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string tenantId = call.Arg<TenantUsersRequest>().TenantId;
                TenantDetail detail = tenantId == beta.TenantId ? beta : alpha;
                return Task.FromResult(MemberSnapshot(detail) with
                {
                    ProjectionVersion = tenantId == beta.TenantId ? "beta-v1" : "alpha-v1",
                });
            });
        gateway.GetUpdateMetadataProjectionProofAsync(Arg.Any<UpdateTenant>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                proofCancellation = call.ArgAt<CancellationToken>(1);
                proofStarted.TrySetResult();
                return delayedProof.Task;
            });
        ConfigureAuthoritativeAuditCapability(gateway);

        ITenantCommandGateway commandGateway = Substitute.For<ITenantCommandGateway>();
        commandGateway.UpdateTenantAsync(Arg.Any<UpdateTenant>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandSubmissionResult.Accepted("alpha-message", "alpha-correlation")));
        commandGateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 }));

        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, alpha.TenantId));
        cut.WaitForElement("[data-testid='tenants-edit-metadata-flow']");
        EditTenantMetadataFlow alphaFlow = cut.FindComponent<EditTenantMetadataFlow>().Instance;

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Alpha renamed");
        cut.Find("form").Submit();
        await proofStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cut.Render(parameters => parameters.Add(page => page.TenantId, beta.TenantId));
        cut.WaitForElement("[data-testid='tenants-edit-metadata-flow']");
        EditTenantMetadataFlow betaFlow = cut.FindComponent<EditTenantMetadataFlow>().Instance;
        ReferenceEquals(alphaFlow, betaFlow).ShouldBeFalse();
        proofCancellation.IsCancellationRequested.ShouldBeTrue();

        delayedProof.SetResult(ReadyWithSafeConfiguration(
            alpha with { Name = "Alpha renamed" },
            ProjectionLifecycleState.Current,
            "alpha-v2"));
        await Task.Yield();

        cut.WaitForAssertion(() =>
        {
            betaFlow.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Idle);
            betaFlow.Snapshot.Intent.ShouldBeNull();
            betaFlow.Snapshot.MessageId.ShouldBeNull();
            betaFlow.Snapshot.CorrelationId.ShouldBeNull();
            betaFlow.Snapshot.LastConfirmedDetailProjection.ShouldNotBeNull().TenantId.ShouldBe(beta.TenantId);
            betaFlow.Snapshot.LastConfirmedName.ShouldBe("Beta");
            betaFlow.ProjectionVersionProvider.ShouldNotBeNull().Invoke().ShouldBe("beta-v1");
            betaFlow.IsCommandSurfaceAvailable.ShouldBeTrue();
        });
        cut.Find("[data-testid='tenants-edit-metadata-confirmed']").TextContent.ShouldContain("Beta");
        cut.Find("[data-testid='tenants-edit-metadata-confirmed']").TextContent.ShouldNotContain("Alpha renamed");
        alphaFlow.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.Confirmed);
    }

    [Fact]
    public async Task Route_change_discards_a_delayed_metadata_audit_proof_and_cannot_confirm_the_new_tenant()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        TenantDetail alpha = Detail("tenant.alpha");
        TenantDetail beta = Detail("tenant.beta") with
        {
            Name = "Beta",
            Description = "Tenant beta description",
        };
        var auditStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var delayedAudit = new TaskCompletionSource<TenantAuditSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        TenantAuditRequest? alphaAuditRequest = null;
        CancellationToken auditCancellation = default;

        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string tenantId = call.Arg<TenantDetailRequest>().TenantId;
                TenantDetail detail = tenantId == beta.TenantId ? beta : alpha;
                string projectionVersion = tenantId == beta.TenantId ? "beta-v1" : "alpha-v1";
                return Task.FromResult(ReadyWithSafeConfiguration(
                    detail,
                    ProjectionLifecycleState.Current,
                    projectionVersion));
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string tenantId = call.Arg<TenantUsersRequest>().TenantId;
                TenantDetail detail = tenantId == beta.TenantId ? beta : alpha;
                return Task.FromResult(MemberSnapshot(detail) with
                {
                    ProjectionVersion = tenantId == beta.TenantId ? "beta-v1" : "alpha-v1",
                });
            });
        gateway.GetUpdateMetadataProjectionProofAsync(Arg.Any<UpdateTenant>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                alpha,
                ProjectionLifecycleState.Current,
                "alpha-v2")));
        ConfigureAuthoritativeAuditCapability(gateway);
        gateway.GetTenantAuditAsync(
                Arg.Is<TenantAuditRequest>(request => request.From.HasValue),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                alphaAuditRequest = call.ArgAt<TenantAuditRequest>(0);
                auditCancellation = call.ArgAt<CancellationToken>(2);
                auditStarted.TrySetResult();
                return delayedAudit.Task;
            });

        ITenantCommandGateway commandGateway = Substitute.For<ITenantCommandGateway>();
        commandGateway.UpdateTenantAsync(Arg.Any<UpdateTenant>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandSubmissionResult.Accepted("alpha-message", "alpha-correlation")));
        commandGateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 }));

        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, alpha.TenantId));
        cut.WaitForElement("[data-testid='tenants-edit-metadata-flow']");
        EditTenantMetadataFlow alphaFlow = cut.FindComponent<EditTenantMetadataFlow>().Instance;

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("form").Submit();
        await auditStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));

        cut.Render(parameters => parameters.Add(page => page.TenantId, beta.TenantId));
        cut.WaitForElement("[data-testid='tenants-edit-metadata-flow']");
        EditTenantMetadataFlow betaFlow = cut.FindComponent<EditTenantMetadataFlow>().Instance;
        ReferenceEquals(alphaFlow, betaFlow).ShouldBeFalse();
        auditCancellation.IsCancellationRequested.ShouldBeTrue();

        TenantAuditRequest completedRequest = alphaAuditRequest.ShouldNotBeNull();
        delayedAudit.SetResult(MetadataAuditSnapshot(
            completedRequest,
            MetadataAuditRow(
                "alpha-message",
                alpha.TenantId,
                completedRequest.From.ShouldNotBeNull())));
        await Task.Yield();

        cut.WaitForAssertion(() =>
        {
            betaFlow.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Idle);
            betaFlow.Snapshot.Intent.ShouldBeNull();
            betaFlow.Snapshot.MessageId.ShouldBeNull();
            betaFlow.Snapshot.CorrelationId.ShouldBeNull();
            betaFlow.Snapshot.LastConfirmedDetailProjection.ShouldNotBeNull().TenantId.ShouldBe(beta.TenantId);
            betaFlow.Snapshot.LastConfirmedName.ShouldBe("Beta");
            betaFlow.ProjectionVersionProvider.ShouldNotBeNull().Invoke().ShouldBe("beta-v1");
            betaFlow.IsCommandSurfaceAvailable.ShouldBeTrue();
        });
        cut.Find("[data-testid='tenants-edit-metadata-confirmed']").TextContent.ShouldContain("Beta");
        alphaFlow.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.Confirmed);
    }

    [Theory]
    [InlineData("unrelated")]
    [InlineData("mismatched-event")]
    [InlineData("mismatched-tenant")]
    [InlineData("stale")]
    [InlineData("unauthorized")]
    [InlineData("incomplete")]
    [InlineData("absent")]
    [InlineData("ambiguous-cross-page")]
    [InlineData("version-drift-cross-page")]
    [InlineData("page-exhaustion")]
    public void Detail_page_does_not_confirm_identical_metadata_from_non_authoritative_audit_evidence(
        string evidenceKind)
        => AssertMetadataAuditProofDoesNotConfirm(evidenceKind);

    [Fact]
    public void Detail_page_rejects_stale_metadata_projection_proof()
        => AssertMetadataProjectionProofDoesNotConfirm(renamed => TenantDetailSnapshot.Stale(
            renamed,
            "proof-etag",
            ProjectionLifecycleState.Current,
            "projection-v2").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha")));

    [Fact]
    public void Detail_page_rejects_unknown_freshness_metadata_projection_proof()
        => AssertMetadataProjectionProofDoesNotConfirm(renamed => TenantDetailSnapshot.Ready(
            renamed,
            "proof-etag",
            ReadModelFreshnessState.Unknown,
            ProjectionLifecycleState.Current,
            "projection-v2").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha")));

    [Fact]
    public void Detail_page_rejects_non_current_lifecycle_metadata_projection_proof()
        => AssertMetadataProjectionProofDoesNotConfirm(renamed => TenantDetailSnapshot.Ready(
            renamed,
            "proof-etag",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Rebuilding,
            "projection-v2").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha")));

    [Fact]
    public void Detail_page_rejects_unversioned_metadata_projection_proof()
        => AssertMetadataProjectionProofDoesNotConfirm(renamed => ReadyWithSafeConfiguration(
            renamed,
            ProjectionLifecycleState.Current,
            projectionVersion: null));

    [Fact]
    public void Detail_page_rejects_disabled_tenant_metadata_projection_proof()
        => AssertMetadataProjectionProofDoesNotConfirm(renamed => ReadyWithSafeConfiguration(
            renamed with { Status = TenantStatus.Disabled },
            ProjectionLifecycleState.Current,
            "projection-v2"));

    private void AssertMetadataAuditProofDoesNotConfirm(string evidenceKind)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        TenantDetail original = Detail("tenant.alpha");

        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                ReadyWithSafeConfiguration(original, ProjectionLifecycleState.Current, "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>().TenantId))));
        gateway.GetUpdateMetadataProjectionProofAsync(Arg.Any<UpdateTenant>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                ReadyWithSafeConfiguration(original, ProjectionLifecycleState.Current, "projection-v2")));
        ConfigureAuthoritativeAuditCapability(gateway);
        ConfigureMetadataAuditProof(gateway, request =>
        {
            DateTimeOffset attemptStartedAtUtc = request.From.ShouldNotBeNull();
            TenantAuditRow row = MetadataAuditRow("message-1", request.TenantId, attemptStartedAtUtc);
            return evidenceKind switch
            {
                "unrelated" => MetadataAuditSnapshot(
                    request,
                    row with { EventReference = "message-from-another-update" }),
                "mismatched-event" => MetadataAuditSnapshot(
                    request,
                    row with { EventType = "TenantEnabled", Outcome = "TenantEnabled" }),
                "mismatched-tenant" => MetadataAuditSnapshot(
                    request,
                    row with { TenantId = "tenant.other", Target = "tenant.other", Scope = "tenant.other" }),
                "stale" => MetadataAuditSnapshot(
                    request,
                    row with { Freshness = ReadModelFreshnessState.Stale }),
                "unauthorized" => TenantAuditSnapshot.Unauthorized(request),
                "incomplete" => MetadataAuditSnapshot(request, row, hasMore: true),
                "ambiguous-cross-page" => string.IsNullOrWhiteSpace(request.Cursor)
                    ? MetadataAuditSnapshot(request, row, hasMore: true)
                    : MetadataAuditSnapshot(request, row with { Timestamp = attemptStartedAtUtc.AddSeconds(1) }),
                "version-drift-cross-page" => string.IsNullOrWhiteSpace(request.Cursor)
                    ? MetadataAuditSnapshot(
                        request,
                        row with { EventReference = "message-from-another-update" },
                        hasMore: true,
                        projectionVersion: "audit-proof-v2")
                    : MetadataAuditSnapshot(
                        request,
                        row,
                        projectionVersion: "audit-proof-v3"),
                "page-exhaustion" => MetadataAuditSnapshot(
                    request,
                    row with { EventReference = "message-from-another-update" },
                    hasMore: true),
                "absent" => TenantAuditSnapshot.Empty(
                    isAuthorizationScoped: true,
                    ReadModelFreshnessState.Current,
                    eTag: "audit-proof-etag",
                    request) with
                {
                    CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                    Lifecycle = ProjectionLifecycleState.Current,
                    ProjectionVersion = "audit-proof-v2",
                },
                _ => throw new ArgumentOutOfRangeException(nameof(evidenceKind), evidenceKind, null),
            };
        });

        ITenantCommandGateway commandGateway = Substitute.For<ITenantCommandGateway>();
        commandGateway.UpdateTenantAsync(Arg.Any<UpdateTenant>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandSubmissionResult.Accepted("message-1", "correlation-1")));
        commandGateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 }));

        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-edit-metadata-flow']");

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("form").Submit();

        EditTenantMetadataFlow metadataFlow = cut.FindComponent<EditTenantMetadataFlow>().Instance;
        cut.WaitForAssertion(() => metadataFlow.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        metadataFlow.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.Confirmed);
        metadataFlow.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.AlreadyApplied);
        metadataFlow.Snapshot.SafeMessageKey.ShouldBe("Tenants.EditMetadata.Confirm.UnableToVerify.MissingProvenance");

        // Non-authoritative audit evidence is a provenance failure, not a failed audit read: the audit dimension
        // keeps what the Completed status established.
        metadataFlow.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);
        metadataFlow.Snapshot.LastConfirmedName.ShouldBe("Alpha");
        metadataFlow.IsCommandSurfaceAvailable.ShouldBeFalse();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();

        if (evidenceKind is "ambiguous-cross-page" or "version-drift-cross-page")
        {
            // Both pages must actually be walked -- otherwise the guard under test never runs.
            _ = gateway.Received(1).GetTenantAuditAsync(
                Arg.Is<TenantAuditRequest>(r => r.Category == AuditEventCategory.Administrative && string.IsNullOrWhiteSpace(r.Cursor)),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>());
            _ = gateway.Received(1).GetTenantAuditAsync(
                Arg.Is<TenantAuditRequest>(r => r.Category == AuditEventCategory.Administrative && r.Cursor == "opaque-next-page"),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>());
        }
        else if (evidenceKind is "page-exhaustion")
        {
            // Bounded by MetadataAuditProofMaximumPageCount (50): the walk must stop there, not hang forever
            // on a source that always reports HasMore.
            _ = gateway.Received(50).GetTenantAuditAsync(
                Arg.Is<TenantAuditRequest>(r => r.Category == AuditEventCategory.Administrative),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>());
        }
    }

    private void AssertMetadataProjectionProofDoesNotConfirm(
        Func<TenantDetail, TenantDetailSnapshot> proofFactory)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        TenantDetail original = Detail("tenant.alpha");
        TenantDetail renamed = original with { Name = "Alpha renamed" };

        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(
                ReadyWithSafeConfiguration(original, ProjectionLifecycleState.Current, "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>().TenantId))));
        gateway.GetUpdateMetadataProjectionProofAsync(Arg.Any<UpdateTenant>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(proofFactory(renamed)));
        ConfigureAuthoritativeAuditCapability(gateway);

        ITenantCommandGateway commandGateway = Substitute.For<ITenantCommandGateway>();
        commandGateway.UpdateTenantAsync(Arg.Any<UpdateTenant>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantCommandSubmissionResult.Accepted("message-1", "correlation-1")));
        commandGateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 }));

        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(commandGateway);
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-edit-metadata-flow']");

        EditTenantMetadataFlow metadataFlow = cut.FindComponent<EditTenantMetadataFlow>().Instance;
        Func<string?> projectionVersionProvider = metadataFlow.ProjectionVersionProvider.ShouldNotBeNull();
        projectionVersionProvider().ShouldBe("projection-v1");

        cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
        cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Alpha renamed");
        cut.Find("form").Submit();

        cut.WaitForAssertion(() => metadataFlow.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        metadataFlow.Snapshot.State.ShouldNotBe(TenantCommandLifecycleState.Confirmed);
        metadataFlow.Snapshot.SafeMessageKey.ShouldBe("Tenants.EditMetadata.Confirm.UnableToVerify.MissingProvenance");
        metadataFlow.Snapshot.LastConfirmedName.ShouldBe("Alpha");
        projectionVersionProvider().ShouldBe("projection-v1");
        metadataFlow.IsCommandSurfaceAvailable.ShouldBeFalse();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>()
            .IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();

        // A non-authoritative proof reaches the flow as no proof: a provenance failure, not a failed audit read,
        // so the audit record the Completed status established stays pending.
        metadataFlow.Snapshot.AuditState.ShouldBe(TenantCommandAuditState.AuditPending);
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-edit-metadata-audit'] [data-testid='tenants-audit-availability']")
            .GetAttribute("data-state").ShouldBe("pending"));
        _ = gateway.Received(1).GetUpdateMetadataProjectionProofAsync(
            Arg.Is<UpdateTenant>(request => request.TenantId == "tenant.alpha"),
            Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    public async Task Management_releases_retained_only_activity_after_refused_adoption_expires(bool rerender, bool childFirst)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using var tracker = new TenantRemoveConfigurationAttemptTracker(() => now);
        TenantRemoveConfigurationIntent intent = new("tenant.alpha", "billing", "billing.mode");
        TenantRemoveConfigurationPreview preview = TenantRemoveConfigurationPreview.Create(
            intent, TenantStatus.Active, TenantRemoveConfigurationCurrentState.Present,
            ReadModelFreshnessState.Current, ProjectionLifecycleState.Current, "tenant-sequence:41", true);
        (string messageId, DateTimeOffset startedAt) = tracker.BeginDispatch(intent, "tenant-sequence:41", now);
        tracker.Remember(TenantRemoveConfigurationCommandSnapshot.Idle().Previewed(preview)
            .RequestSent(preview, messageId, startedAt)
            .Accepted(TenantCommandSubmissionResult.Accepted(messageId, messageId))).ShouldBeTrue();
        Services.AddSingleton(tracker);
        RegisterComponentServices();
        int acquisitionCalls = 0;
        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            intent.TenantId, TenantStatus.Active, false, ["billing"],
            [new TenantConfigurationSafeRow("billing", "billing.mode", "trial")],
            TenantConfigurationAuthorityState.TenantOwner);
        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Context, context)
            .Add(p => p.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(p => p.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration))
            .Add(p => p.IsCommandSurfaceAvailable, false)
            .Add(p => p.RemoveCommandActivityLease, active =>
            {
                if (active)
                {
                    acquisitionCalls++;
                }

                return Task.FromResult(!active);
            }));
        cut.WaitForAssertion(() => acquisitionCalls.ShouldBe(1));
        cut.FindComponent<RemoveTenantConfigurationFlow>().Instance.Snapshot.MessageId.ShouldBe(messageId);
        cut.FindAll("[data-testid='tenants-config-management-unavailable']").ShouldBeEmpty();

        // Pruning on a tracker consumer's thread emits the existing expiry notification. No child owns
        // accepted activity, and no parameter update is needed in the notification-only case.
        int focusCalls = FocusedElementIds().Count;
        now += TenantRemoveConfigurationCommandSnapshot.MaximumRetainedAttemptDuration;
        if (childFirst)
        {
            RemoveTenantConfigurationFlow flow = cut.FindComponent<RemoveTenantConfigurationFlow>().Instance;
            await cut.InvokeAsync(() => flow.ExpireRetainedAttemptAsync(now));
        }
        else
        {
            await Task.Run(() => tracker.HasPendingOwnership(intent.TenantId).ShouldBeFalse());
        }
        if (rerender)
        {
            cut.Render();
        }

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-config-management-unavailable']")
            .TextContent.Trim().ShouldBe(Services.GetRequiredService<IStringLocalizer<TenantsResources>>()
                ["Tenants.Configuration.Management.Unavailable.CommandSurface"].Value));
        cut.FindComponents<RemoveTenantConfigurationFlow>().ShouldBeEmpty();
        acquisitionCalls.ShouldBe(1);
        FocusedElementIds().Count.ShouldBe(focusCalls + 1);
        FocusedElementIds()[^1].ShouldBe(CapturedElementReferenceId(cut.Instance, "_headingElement"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Management_retention_expiry_preserves_activity_owned_by_each_flow(bool setOwnsActivity)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using var tracker = new TenantRemoveConfigurationAttemptTracker(() => now);
        TenantConfigurationManagementContext context = RememberManagementRemoval(tracker, now);
        Services.AddSingleton(tracker);
        RegisterComponentServices();
        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Context, context)
            .Add(p => p.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(p => p.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration))
            .Add(p => p.IsCommandSurfaceAvailable, false)
            .Add(p => p.RemoveCommandActivityLease, active => Task.FromResult(!active || !setOwnsActivity)));
        RemoveTenantConfigurationFlow remove = cut.FindComponent<RemoveTenantConfigurationFlow>().Instance;
        SetTenantConfigurationFlow set = cut.FindComponent<SetTenantConfigurationFlow>().Instance;
        if (setOwnsActivity)
        {
            await cut.InvokeAsync(() => set.CommandActivityLease.ShouldNotBeNull()(true));
        }
        else
        {
            cut.WaitForAssertion(() => remove.Snapshot.State.ShouldBe(TenantCommandLifecycleState.UnableToVerify));
        }

        now += TenantRemoveConfigurationCommandSnapshot.MaximumRetainedAttemptDuration;
        await Task.Run(() => tracker.HasPendingOwnership(context.TenantId).ShouldBeFalse());
        await cut.InvokeAsync(() => { });
        cut.Render();

        cut.FindComponent<SetTenantConfigurationFlow>().Instance.ShouldBeSameAs(set);
        cut.FindComponent<RemoveTenantConfigurationFlow>().Instance.ShouldBeSameAs(remove);
        cut.FindAll("[data-testid='tenants-config-management-unavailable']").ShouldBeEmpty();
        Func<bool, Task<bool>> lease = setOwnsActivity
            ? set.CommandActivityLease.ShouldNotBeNull()
            : remove.CommandActivityLease.ShouldNotBeNull();
        await cut.InvokeAsync(() => lease(false));
        cut.Render();
        cut.Find("[data-testid='tenants-config-management-unavailable']");
    }

    [Fact]
    public async Task Management_ignores_expiry_for_another_tenant()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using var tracker = new TenantRemoveConfigurationAttemptTracker(() => now);
        _ = RememberManagementRemoval(tracker, now, "tenant.beta");
        Services.AddSingleton(tracker);
        RegisterComponentServices();
        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            "tenant.alpha", TenantStatus.Active, false, ["billing"],
            [new TenantConfigurationSafeRow("billing", "billing.mode", "trial")],
            TenantConfigurationAuthorityState.TenantOwner);
        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Context, context)
            .Add(p => p.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(p => p.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration)));
        SetTenantConfigurationFlow set = cut.FindComponent<SetTenantConfigurationFlow>().Instance;
        int renders = cut.RenderCount;

        now += TenantRemoveConfigurationCommandSnapshot.MaximumRetainedAttemptDuration;
        await Task.Run(() => tracker.HasPendingOwnership("tenant.beta").ShouldBeFalse());
        await cut.InvokeAsync(() => { });

        cut.RenderCount.ShouldBe(renders);
        cut.FindComponent<SetTenantConfigurationFlow>().Instance.ShouldBeSameAs(set);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Management_queued_expiry_preserves_replacement_target_and_current_tenant(bool switchTenant)
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using var tracker = new TenantRemoveConfigurationAttemptTracker(() => now);
        TenantConfigurationManagementContext context = RememberManagementRemoval(tracker, now);
        Services.AddSingleton(tracker);
        RegisterComponentServices();
        int acquisitions = 0;
        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Context, context)
            .Add(p => p.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(p => p.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration))
            .Add(p => p.IsCommandSurfaceAvailable, false)
            .Add(p => p.RemoveCommandActivityLease, active =>
            {
                if (active) acquisitions++;
                return Task.FromResult(!active);
            }));
        cut.WaitForAssertion(() => acquisitions.ShouldBe(1));
        using var dispatcherHeld = new ManualResetEventSlim();
        using var releaseDispatcher = new ManualResetEventSlim();
        TenantConfigurationManagementContext? replacement = null;
        Task blocker = Task.Run(() => Renderer.Dispatcher.InvokeAsync(() =>
        {
            dispatcherHeld.Set();
            releaseDispatcher.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            if (switchTenant)
            {
                TenantConfigurationManagementContext next = replacement.ShouldNotBeNull();
                cut.Render(parameters => parameters
                    .Add(p => p.Context, next)
                    .Add(p => p.SetEvidence, ManagementEvidence(next, TenantHighImpactAction.SetConfiguration))
                    .Add(p => p.RemoveEvidence, ManagementEvidence(next, TenantHighImpactAction.RemoveConfiguration)));
            }
        }));
        try
        {
            dispatcherHeld.Wait(TimeSpan.FromSeconds(5)).ShouldBeTrue();
            now += TenantRemoveConfigurationCommandSnapshot.MaximumRetainedAttemptDuration;
            await Task.Run(() => tracker.HasPendingOwnership(context.TenantId).ShouldBeFalse());
            replacement = RememberManagementRemoval(tracker, now,
                switchTenant ? "tenant.beta" : context.TenantId, "billing.region");
        }
        finally
        {
            releaseDispatcher.Set();
            await blocker.WaitAsync(TimeSpan.FromSeconds(5));
        }

        await Renderer.Dispatcher.InvokeAsync(() => { });
        cut.WaitForAssertion(() =>
        {
            RemoveTenantConfigurationFlow flow = cut.FindComponent<RemoveTenantConfigurationFlow>().Instance;
            flow.TargetKey.ShouldBe("billing.region");
            flow.Snapshot.Intent.ShouldNotBeNull().FullKey.ShouldBe(flow.TargetKey);
            flow.Snapshot.Intent.TenantId.ShouldBe(switchTenant ? "tenant.beta" : "tenant.alpha");
            tracker.Find(flow.Snapshot.Intent.TenantId).ShouldNotBeNull().MessageId.ShouldBe(flow.Snapshot.MessageId);
        });
        cut.FindAll("[data-testid='tenants-config-management-unavailable']").ShouldBeEmpty();
        Renderer.UnhandledException.IsCompleted.ShouldBeFalse();
    }

    [Fact]
    public async Task Management_disposal_releases_its_tracker_subscription()
    {
        DateTimeOffset now = DateTimeOffset.UtcNow;
        using var tracker = new TenantRemoveConfigurationAttemptTracker(() => now);
        TenantConfigurationManagementContext context = RememberManagementRemoval(tracker, now);
        Services.AddSingleton(tracker);
        RegisterComponentServices();
        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Context, context)
            .Add(p => p.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(p => p.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration))
            .Add(p => p.RemoveCommandActivityLease, active => Task.FromResult(!active)));
        TenantConfigurationManagement management = cut.Instance;
        FieldInfo eventField = typeof(TenantRemoveConfigurationAttemptTracker)
            .GetField("OwnershipExpired", BindingFlags.Instance | BindingFlags.NonPublic).ShouldNotBeNull();
        Delegate[] Subscribers() => (eventField.GetValue(tracker) as Delegate)?.GetInvocationList() ?? [];
        Subscribers().Count(handler => ReferenceEquals(handler.Target, management)).ShouldBe(1);
        Delegate callback = Subscribers().Single(handler => ReferenceEquals(handler.Target, management));

        int renders = cut.RenderCount;
        await DisposeComponentsAsync();

        Subscribers().ShouldNotContain(handler => ReferenceEquals(handler.Target, management));
        // A tracker may already have captured the callback when disposal removes it.
        await Task.Run(() => ((Action<string>)callback)(context.TenantId));
        await Renderer.Dispatcher.InvokeAsync(() => { });
        cut.RenderCount.ShouldBe(renders);
        Renderer.UnhandledException.IsCompleted.ShouldBeFalse();
    }

    private static TenantConfigurationManagementContext RememberManagementRemoval(
        TenantRemoveConfigurationAttemptTracker tracker,
        DateTimeOffset now,
        string tenantId = "tenant.alpha",
        string key = "billing.mode")
    {
        TenantRemoveConfigurationIntent intent = new(tenantId, "billing", key);
        TenantRemoveConfigurationPreview preview = TenantRemoveConfigurationPreview.Create(
            intent, TenantStatus.Active, TenantRemoveConfigurationCurrentState.Present,
            ReadModelFreshnessState.Current, ProjectionLifecycleState.Current, "tenant-sequence:41", true);
        (string messageId, DateTimeOffset startedAt) = tracker.BeginDispatch(intent, "tenant-sequence:41", now);
        tracker.Remember(TenantRemoveConfigurationCommandSnapshot.Idle().Previewed(preview)
            .RequestSent(preview, messageId, startedAt)
            .Accepted(TenantCommandSubmissionResult.Accepted(messageId, messageId))).ShouldBeTrue();
        return TenantConfigurationManagementContext.Available(
            tenantId, TenantStatus.Active, false, ["billing"],
            [new TenantConfigurationSafeRow("billing", key, "trial")],
            TenantConfigurationAuthorityState.TenantOwner);
    }

    [Fact]
    public void Management_closes_an_untouched_remove_flow_when_its_target_row_disappears()
    {
        // The stale-target reset still applies while no command has been submitted: nothing is settling,
        // so a flow pointing at a row that no longer exists is just stale.
        RegisterComponentServices();
        TenantConfigurationSafeRow row = new("billing", "billing.mode", "trial");
        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            "tenant.alpha", TenantStatus.Active, false, ["billing"], [row],
            TenantConfigurationAuthorityState.TenantOwner);
        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(p => p.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration)));

        cut.Find("[data-testid='tenants-config-management-remove-open']").Click();
        cut.Find("[data-testid='tenants-config-remove-flow']");

        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, TenantConfigurationManagementContext.Available(
                "tenant.alpha", TenantStatus.Active, false, ["billing"], [],
                TenantConfigurationAuthorityState.TenantOwner))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.FindAll("[data-testid='tenants-config-remove-flow']").ShouldBeEmpty();
    }

    [Fact]
    public async Task Management_switching_tenants_drops_every_open_interaction_even_mid_command()
    {
        RegisterComponentServices();
        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            "tenant.alpha", TenantStatus.Active, false, ["billing"],
            [new TenantConfigurationSafeRow("billing", "billing.mode", "trial")],
            TenantConfigurationAuthorityState.TenantOwner);
        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, context)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(p => p.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration)));

        cut.Find("[data-testid='tenants-config-management-remove-open']").Click();
        IRenderedComponent<RemoveTenantConfigurationFlow> flow = cut.FindComponent<RemoveTenantConfigurationFlow>();
        await cut.InvokeAsync(() => flow.Instance.CommandActivityLease!(true));

        cut.Render(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.Context, TenantConfigurationManagementContext.Available(
                "tenant.beta", TenantStatus.Active, false, ["billing"],
                [new TenantConfigurationSafeRow("billing", "billing.mode", "trial")],
                TenantConfigurationAuthorityState.TenantOwner))
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current));

        cut.FindAll("[data-testid='tenants-config-remove-flow']").ShouldBeEmpty();
    }

    [Fact]
    public void Configuration_management_is_a_sibling_landmark_with_safe_targets_and_target_specific_actions()
    {
        RegisterComponentServices();
        TenantConfigurationSafeRow row = new("billing", "billing.mode", "trial");
        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            "tenant.alpha",
            TenantStatus.Active,
            false,
            ["billing"],
            [row],
            TenantConfigurationAuthorityState.TenantOwner);

        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.Context, context)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(component => component.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration))
            .Add(component => component.RemovePreviewEvidenceProvider, intent => Task.FromResult(
                TenantRemoveConfigurationPreview.Create(
                    intent,
                    TenantStatus.Active,
                    TenantRemoveConfigurationCurrentState.Present,
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current,
                    "tenant-sequence:10",
                    isAuthorized: true))));

        cut.Find("[data-testid='tenants-config-management-section']");
        cut.Find("[data-testid='tenants-config-set-flow']");
        cut.Find("[data-testid='tenants-config-management-targets']").GetAttribute("aria-label").ShouldNotBeNullOrWhiteSpace();
        IElement removeAction = cut.Find("[data-testid='tenants-config-management-remove-open']");
        string accessibleName = removeAction.GetAttribute("aria-label") ?? string.Empty;
        accessibleName.ShouldContain("billing.mode");

        removeAction.Click();
        cut.Find("[data-testid='tenants-config-remove-flow']");
        cut.Find("[data-testid='tenants-config-remove-cancel']").Click();
        cut.FindAll("[data-testid='tenants-config-remove-flow']").ShouldBeEmpty();
    }

    [Fact]
    public void Configuration_management_returns_focus_to_the_launching_control_when_the_remove_flow_closes()
    {
        // Restores the focus-return regression guard dropped by the read/management split, which
        // test-summary.md still claimed was covered. The launching control is refocused through an
        // ElementReference, so a JS focus invocation is the observable evidence.
        RegisterComponentServices();
        TenantConfigurationSafeRow row = new("billing", "billing.mode", "trial");
        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            "tenant.alpha",
            TenantStatus.Active,
            false,
            ["billing"],
            [row],
            TenantConfigurationAuthorityState.TenantOwner);

        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.Context, context)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(component => component.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration))
            .Add(component => component.RemovePreviewEvidenceProvider, intent => Task.FromResult(
                TenantRemoveConfigurationPreview.Create(
                    intent,
                    TenantStatus.Active,
                    TenantRemoveConfigurationCurrentState.Present,
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current,
                    "tenant-sequence:10",
                    isAuthorized: true))));

        cut.Find("[data-testid='tenants-config-management-remove-open']").Click();
        int focusCallsBeforeClose = FocusedElementIds().Count;

        cut.Find("[data-testid='tenants-config-remove-cancel']").Click();

        // The row is still present here, so focus must return to ITS launch control -- the other of the two
        // focus paths, and the one the sibling test above proves is not taken when the row is gone.
        IReadOnlyList<string> focusedAfterCancel = FocusedElementIds();
        focusedAfterCancel.Count.ShouldBe(focusCallsBeforeClose + 1);
        focusedAfterCancel[^1].ShouldBe(CapturedLaunchElementReferenceId(cut.Instance, "billing.mode"));
    }

    [Fact]
    public void Configuration_management_can_remove_an_authorized_key_that_contains_no_separator()
    {
        // Grant `P` authorizes exact key `P` as well as `P.*`, so a dotless key is a legitimate safe
        // row. The remove flow derived its namespace as empty for such a key and opened permanently
        // blocked on scope, while the set flow handled the same key correctly.
        RegisterComponentServices();
        TenantConfigurationSafeRow row = new("maintenance", "maintenance", "on");
        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            "tenant.alpha",
            TenantStatus.Active,
            false,
            ["maintenance"],
            [row],
            TenantConfigurationAuthorityState.TenantOwner);

        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.Context, context)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.SetEvidence, ManagementEvidence(context, TenantHighImpactAction.SetConfiguration))
            .Add(component => component.RemoveEvidence, ManagementEvidence(context, TenantHighImpactAction.RemoveConfiguration))
            .Add(component => component.RemovePreviewEvidenceProvider, intent => Task.FromResult(
                TenantRemoveConfigurationPreview.Create(
                    intent,
                    TenantStatus.Active,
                    TenantRemoveConfigurationCurrentState.Present,
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current,
                    "tenant-sequence:10",
                    isAuthorized: true))));

        cut.Find("[data-testid='tenants-config-management-remove-open']").Click();

        cut.Find("[data-testid='tenants-config-remove-flow']");
        cut.WaitForElement("[data-testid='tenants-config-remove-preview-namespace']");
        cut.FindAll("[data-testid='tenants-config-remove-preview-blocked']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-config-remove-preview-namespace']").TextContent.ShouldContain("maintenance");
    }

    [Fact]
    public void Configuration_management_distinguishes_an_unverifiable_policy_from_a_policy_that_grants_nothing()
    {
        // A valid policy granting this caller nothing is not a verification failure. Reporting it as one
        // contradicted the sibling read landmark, which correctly showed authorization-safe empty.
        RegisterComponentServices();

        IRenderedComponent<TenantConfigurationManagement> noScope = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.Context, TenantConfigurationManagementContext.Available(
                "tenant.alpha",
                TenantStatus.Active,
                false,
                [],
                [],
                TenantConfigurationAuthorityState.TenantOwner))
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current));

        string noScopeText = noScope.Find("[data-testid='tenants-config-management-unavailable']").TextContent;
        noScopeText.ShouldContain("no configuration namespace is granted", Case.Insensitive);
        noScopeText.ShouldNotContain("cannot be verified", Case.Insensitive);

        IRenderedComponent<TenantConfigurationManagement> unverifiable = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.Context, TenantConfigurationManagementContext.Unavailable("tenant.alpha"))
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current));

        unverifiable.Find("[data-testid='tenants-config-management-unavailable']").TextContent
            .ShouldContain("cannot be verified", Case.Insensitive);
    }

    [Fact]
    public void Configuration_management_keeps_valid_empty_and_unavailable_states_mutually_exclusive()
    {
        RegisterComponentServices();
        TenantConfigurationManagementContext empty = TenantConfigurationManagementContext.Available(
            "tenant.alpha",
            TenantStatus.Active,
            false,
            ["billing"],
            [],
            TenantConfigurationAuthorityState.TenantOwner);

        IRenderedComponent<TenantConfigurationManagement> validEmpty = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.Context, empty)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current));

        validEmpty.Find("[data-testid='tenants-config-management-empty']");
        validEmpty.Find("[data-testid='tenants-config-set-flow']");
        validEmpty.FindAll("[data-testid='tenants-config-management-unavailable']").ShouldBeEmpty();

        IRenderedComponent<TenantConfigurationManagement> policyUnavailable = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.Context, TenantConfigurationManagementContext.Unavailable("tenant.alpha"))
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current));

        policyUnavailable.Find("[data-testid='tenants-config-management-unavailable']");
        policyUnavailable.FindAll("[data-testid='tenants-config-management-empty']").ShouldBeEmpty();
        policyUnavailable.FindAll("[data-testid='tenants-config-set-flow']").ShouldBeEmpty();

        IRenderedComponent<TenantConfigurationManagement> stale = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(component => component.Context, empty)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Stale)
            .Add(component => component.Freshness, ReadModelFreshnessState.Stale));

        stale.Find("[data-testid='tenants-config-management-unavailable']");
        stale.FindAll("[data-testid='tenants-config-management-empty']").ShouldBeEmpty();
        stale.FindAll("[data-testid='tenants-config-set-flow']").ShouldBeEmpty();
    }

    [Fact]
    public void Configuration_management_explains_noncurrent_projection_lifecycle_and_hides_mutation_flows()
    {
        RegisterComponentServices();
        TenantConfigurationManagementContext context = TenantConfigurationManagementContext.Available(
            "tenant.alpha",
            TenantStatus.Active,
            false,
            ["billing"],
            [new TenantConfigurationSafeRow("billing", "billing.mode", "trial")],
            TenantConfigurationAuthorityState.TenantOwner);

        IRenderedComponent<TenantConfigurationManagement> cut = Render<TenantConfigurationManagement>(parameters => parameters
            .Add(component => component.Context, context)
            .Add(component => component.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(component => component.Freshness, ReadModelFreshnessState.Current)
            .Add(component => component.Lifecycle, ProjectionLifecycleState.Stale));

        cut.Find("[data-testid='tenants-config-management-unavailable']").TextContent
            .ShouldContain("projection-confirmed lifecycle", Case.Insensitive);
        cut.FindAll("[data-testid='tenants-config-set-flow']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-config-management-remove-open']").ShouldBeEmpty();
    }

    [Fact]
    public void Detail_page_composes_member_access_review_without_replacing_existing_surfaces()
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"))));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-table']");

        // Asserted past the shared prefix: both summary variants begin "{0} members visible on this page",
        // so the previous ShouldContain("2 members") passed on either branch. This fixture builds the detail
        // snapshot with the default Current lifecycle and no projection version, so the evidence is not
        // version-consistent and the owner-context variant is correctly withheld. Both branches are covered
        // by Member_governance_claims_require_current_lifecycle_and_a_stated_matching_projection_version.
        cut.Find("[data-testid='tenants-detail-member-summary']").TextContent
            .ShouldContain("2 members visible on this page. Owner context is unavailable");
        cut.Find("[data-testid='tenants-member-section']").TextContent.ShouldContain("Member access review");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("owner-user");
        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain("billing.mode");
        cut.Markup.ShouldContain("data-testid=\"tenants-member-truth-badge\"");
        cut.Markup.ShouldNotContain("tenants-list-truth-state");
    }

    [Fact]
    public void Detail_page_keeps_freshness_and_projection_lifecycle_as_separate_consumer_bindings()
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"),
            ProjectionLifecycleState.Stale)));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-projection-lifecycle']");

        cut.Find("[data-testid='tenants-detail-truth-state']")
            .GetAttribute("class").ShouldNotBeNull().ShouldContain("truth-state-badge--current");
        cut.Find("[data-testid='tenants-detail-projection-lifecycle']")
            .GetAttribute("class").ShouldNotBeNull().ShouldContain("projection-lifecycle-badge--stale");
        cut.Find("[data-testid='tenants-detail-projection-lifecycle-status']")
            .GetAttribute("role").ShouldBe("status");

        // Badge classes are asserted alongside the localized label, not instead of it: a class name is
        // incidental markup, and asserting it alone is what let the resource keys behind these badges go
        // missing without any test noticing.
        cut.Find("[data-testid='tenants-detail-projection-lifecycle']").TextContent.ShouldContain("Stale");
    }

    [Fact]
    public void Detail_page_escalates_the_lifecycle_region_to_alert_only_when_the_projection_is_unavailable()
    {
        // role="status" is implicitly polite, so an assertive escalation must change the role rather than
        // add a contradicting aria-live. Only the "status" side of that ternary was covered, so collapsing
        // it to a constant survived the whole suite.
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha"),
            ProjectionLifecycleState.Unavailable)));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-projection-lifecycle']");

        cut.Find("[data-testid='tenants-detail-projection-lifecycle-status']")
            .GetAttribute("role").ShouldBe("alert");
        cut.Find("[data-testid='tenants-detail-projection-lifecycle']").TextContent.ShouldContain("Unavailable");
    }

    [Fact]
    public void Detail_page_keeps_member_actions_fail_closed_when_routed_detail_is_stale()
    {
        RegisterServices(_ => Task.FromResult(TenantDetailSnapshot.Stale(Detail("tenant.alpha"), "\"etag\"").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha"))));

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-table']");

        cut.Find("[data-testid='tenants-detail-stale']").TextContent.ShouldContain("stale", Case.Insensitive);
        IElement memberSection = cut.Find("[data-testid='tenants-member-section']");
        memberSection.TextContent.ShouldContain("stale data", Case.Insensitive);
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("owner-user");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("stale data");
        cut.Find("[data-testid='tenants-add-member-flow']").TextContent.ShouldContain("Refresh current tenant detail");
        cut.Find("[data-testid='tenants-add-member-submit']").GetAttribute("disabled").ShouldNotBeNull();
        cut.FindAll("[data-testid='tenants-member-action-slot']")
            .ShouldAllBe(static slot => slot.TextContent.Contains("Unavailable", StringComparison.OrdinalIgnoreCase));
        memberSection.TextContent.ShouldNotContain("Success");
    }

    [Fact]
    public void Member_access_review_renders_literal_members_roles_context_and_accessible_table_semantics()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail(
            "tenant.alpha",
            new Dictionary<string, string>(),
            TenantStatus.Active,
            [
                new TenantMember("OWNER/User.01", TenantRole.TenantOwner),
                new TenantMember("reader-user-with-a-very-long-literal-identifier", TenantRole.TenantReader),
                new TenantMember("contributor-user", TenantRole.TenantContributor),
                new TenantMember("unknown-role-user", TenantRole.Unknown),
            ]);

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));

        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("OWNER/User.01");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("reader-user-with-a-very-long-literal-identifier");
        cut.FindAll("[data-testid='tenants-member-copy-reference']").Count.ShouldBe(4);
        cut.FindAll("[data-testid='tenants-member-copy-reference']").ShouldAllBe(static copy => copy.GetAttribute("data-copy-kind") == "UserId");
        cut.FindAll("[data-testid='tenants-member-row']").Count.ShouldBe(4);
        cut.FindAll("[data-testid='tenants-member-user-id']").Count.ShouldBe(4);
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("User");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("Role");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("Change role");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("Remove member");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("Tenant owner");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("Tenant contributor");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("Tenant reader");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("Unknown role");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("1 visible owner");
        cut.Find("[data-testid='tenants-member-truth-state']").TextContent.ShouldContain("Current");
        cut.Find("[data-testid='tenants-member-truth-badge']").TextContent.ShouldContain("Current");
        cut.Find("[data-testid='tenants-member-projection-lifecycle-status']").GetAttribute("role").ShouldBe("status");
        cut.Find("[data-testid='tenants-member-projection-lifecycle-badge']").TextContent.Trim().ShouldBe("Current");
        cut.Markup.ShouldContain("aria-describedby=\"tenants-member-reasons-0\"");
        cut.Markup.ShouldContain("aria-label=\"Literal member user identifier OWNER/User.01\"");
        cut.Find("[data-testid='tenants-member-reason-list']").GetAttribute("tabindex").ShouldBe("0");
    }

    [Fact]
    public void Disabled_member_audit_refresh_uses_owner_projection_refresh_once()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        int projectionRefreshes = 0;
        int authorityRefreshes = 0;

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail) with { Freshness = ReadModelFreshnessState.Stale })
            .Add(view => view.DetailAuditReturnUrl, "/tenants/tenant.alpha")
            .Add(view => view.OnProjectionRefreshRequested, EventCallback.Factory.Create(this, () => projectionRefreshes++)));
        cut.Instance.AuditAuthorityRefresh = () =>
        {
            authorityRefreshes++;
            return Task.CompletedTask;
        };

        cut.FindAll("[data-testid='tenants-audit-entrypoint-refresh']")[0].Click();

        projectionRefreshes.ShouldBe(1);
        authorityRefreshes.ShouldBe(0);
        cut.Find("[data-testid='tenants-member-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldBeNull();
    }

    [Fact]
    public void Suspended_member_owner_refresh_starts_one_capability_probe()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        var owner = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int ownerRefreshes = 0;
        int capabilityProbes = 0;
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail) with { Freshness = ReadModelFreshnessState.Stale })
            .Add(view => view.DetailAuditReturnUrl, "/tenants/tenant.alpha")
            .Add(view => view.OnProjectionRefreshRequested, EventCallback.Factory.Create(this, async () =>
            {
                ownerRefreshes++;
                await owner.Task.ConfigureAwait(false);
                capabilityProbes++;
            })));
        cut.Instance.AuditAuthorityRefresh = () =>
        {
            capabilityProbes++;
            return Task.CompletedTask;
        };

        cut.FindAll("[data-testid='tenants-audit-entrypoint-refresh']")[0].Click();
        ownerRefreshes.ShouldBe(1);
        capabilityProbes.ShouldBe(0);
        owner.SetResult();

        cut.WaitForAssertion(() => capabilityProbes.ShouldBe(1));
        cut.Find("[data-testid='tenants-member-audit-entrypoint']")
            .ParentElement.ShouldNotBeNull().GetAttribute("href").ShouldBeNull();
    }

    [Fact]
    public void Member_access_review_keeps_lifecycle_badge_independent_of_freshness()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail(
            "tenant.alpha",
            new Dictionary<string, string>(),
            TenantStatus.Active,
            [new TenantMember("owner-user", TenantRole.TenantOwner)]);

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail) with
            {
                Freshness = ReadModelFreshnessState.Current,
                Lifecycle = ProjectionLifecycleState.Stale,
            }));

        cut.Find("[data-testid='tenants-member-truth-badge']").TextContent.ShouldContain("Current");
        cut.Find("[data-testid='tenants-member-projection-lifecycle-badge']").TextContent.Trim().ShouldBe("Stale");
        (cut.Find("[data-testid='tenants-member-projection-lifecycle-badge']").GetAttribute("class") ?? string.Empty)
            .ShouldContain("projection-lifecycle-badge--stale");
        cut.Find("[data-testid='tenants-member-row-projection-lifecycle']").TextContent.Trim().ShouldBe("Stale");
    }

    [Fact]
    public void Member_access_review_associates_every_action_slot_with_its_row_reason_list()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail(
            "tenant.alpha",
            new Dictionary<string, string>(),
            TenantStatus.Active,
            [
                new TenantMember("owner-user", TenantRole.TenantOwner),
                new TenantMember("reader-user", TenantRole.TenantReader),
            ]);

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));

        HashSet<string> reasonListIds = cut.FindAll("[data-testid='tenants-member-reason-list']")
            .Select(static list => list.GetAttribute("id").ShouldNotBeNull())
            .ToHashSet(StringComparer.Ordinal);

        reasonListIds.Count.ShouldBe(2);
        cut.FindAll("[data-testid='tenants-member-action-slot']").Count.ShouldBe(2);
        cut.FindAll("[data-testid='tenants-change-role-open']").Count.ShouldBe(2);
        cut.FindAll("[data-testid='tenants-remove-member-open']").Count.ShouldBe(2);
        foreach (IElement slot in cut.FindAll("[data-testid='tenants-member-action-slot']"))
        {
            string describedBy = slot.GetAttribute("aria-describedby").ShouldNotBeNull();
            reasonListIds.ShouldContain(describedBy);
            slot.GetAttribute("aria-label").ShouldNotBeNull().ShouldContain("unavailable");
        }

        cut.FindAll("[data-testid='tenants-change-role-open']")
            .ShouldAllBe(static button => button.GetAttribute("aria-controls") == "tenants-change-role-flow-region");
        cut.FindAll("[data-testid='tenants-remove-member-open']")
            .ShouldAllBe(static button => button.GetAttribute("aria-controls") == "tenants-remove-member-flow-region");
        cut.FindAll("[data-testid='tenants-member-row']")
            .ShouldAllBe(static row => row.GetAttribute("tabindex") == "0");
    }

    [Theory]
    [InlineData("tenants-change-role-open", "tenants-change-role-flow-region", "tenants-change-role-flow")]
    [InlineData("tenants-remove-member-open", "tenants-remove-member-flow-region", "tenants-remove-member-flow")]
    public void Member_access_review_aria_controls_resolves_to_rendered_region_after_open(
        string launchTestId,
        string regionId,
        string flowTestId)
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));

        IElement launch = cut.FindAll($"[data-testid='{launchTestId}']")[0];
        launch.GetAttribute("aria-controls").ShouldBe(regionId);
        // The controlled region is not rendered until the launcher is activated.
        cut.FindAll($"#{regionId}").ShouldBeEmpty();

        launch.Click();

        // After the FluentStack migration the active region must still expose the id that aria-controls
        // names, so the relationship resolves to a real rendered target (not a dangling reference).
        IElement region = cut.Find($"#{regionId}");
        region.Id.ShouldBe(regionId);
        region.QuerySelector($"[data-testid='{flowTestId}']").ShouldNotBeNull(
            $"aria-controls on {launchTestId} must point to the rendered {regionId} region containing the flow.");
    }

    /// <summary>Real parent cancellation and reopening preserve a failed removal after its retry is refused.</summary>
    /// <param name="faultRelease">Whether the parent activity release callback faults.</param>
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task MemberReviewCancelReopenPreservesRefusedRemovalRetryIdentity(bool faultRelease)
    {
        List<(RemoveUserFromTenant Request, string? Id)> deliveries = [];
        ITenantCommandGateway gateway = Substitute.For<ITenantCommandGateway>();
        gateway.RemoveUserFromTenantAsync(Arg.Any<RemoveUserFromTenant>(), Arg.Any<string?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                deliveries.Add((call.Arg<RemoveUserFromTenant>(), call.ArgAt<string?>(1)));
                return Task.FromResult(TenantCommandSubmissionResult.Failed("Delivery needs recovery") with { MessageId = call.ArgAt<string?>(1) });
            });
        RegisterComponentServices(gateway);
        TenantDetail detail = Detail("tenant.alpha");
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var acquired = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        int releases = 0;
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(p => p
            .Add(x => x.IsRemoveMemberAuthorized, true)
            .Add(x => x.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(x => x.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(x => x.AuditProofCapabilityAvailable, true)
            .Add(x => x.Detail, detail)
            .Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(x => x.Freshness, ReadModelFreshnessState.Current)
            .Add(x => x.Lifecycle, ProjectionLifecycleState.Current)
            .Add(x => x.ProjectionVersion, "tenant-sequence:1")
            .Add(x => x.Members, MemberSnapshot(detail))
            .Add(x => x.CommandActivityLease, active =>
            {
                if (active && ++acquisitions == 2)
                {
                    entered.TrySetResult();
                    return acquired.Task;
                }
                return !active && ++releases == 2 && faultRelease
                    ? Task.FromException<bool>(new InvalidOperationException("Release failed")) : Task.FromResult(true);
            }));
        cut.FindAll("[data-testid='tenants-remove-member-open']")[1].Click();
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("[data-testid='tenants-remove-member-flow'] form").SubmitAsync();
        RemoveTenantMemberFlow originalFlow = cut.FindComponent<RemoveTenantMemberFlow>().Instance;
        TenantRemoveMemberCommandSnapshot original = originalFlow.Snapshot;
        original.State.ShouldBe(TenantCommandLifecycleState.Failed);
        Task retry = cut.Find("[data-testid='tenants-remove-member-flow'] form").SubmitAsync();
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Stale));
        acquired.SetResult(true);
        await retry.WaitAsync(TimeSpan.FromSeconds(5));
        deliveries.Count.ShouldBe(1);
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
        cut.Render(p => p.Add(x => x.Freshness, ReadModelFreshnessState.Current));
        await cut.Find("[data-testid='tenants-remove-member-cancel']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.FindComponents<RemoveTenantMemberFlow>().ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-remove-member-open']")[1].Click();
        RemoveTenantMemberFlow replacement = cut.FindComponent<RemoveTenantMemberFlow>().Instance;
        replacement.ShouldNotBeSameAs(originalFlow);
        replacement.Snapshot.MessageId.ShouldBe(original.MessageId);
        replacement.Snapshot.Intent.ShouldBe(original.Intent);
        replacement.Snapshot.AttemptStartedAtUtc.ShouldBe(original.AttemptStartedAtUtc);
        replacement.Snapshot.BaselineProjectionVersion.ShouldBe(original.BaselineProjectionVersion);
        await cut.Find("[data-testid='tenants-remove-member-flow'] form").SubmitAsync();
        deliveries.Count.ShouldBe(1);
        acquisitions.ShouldBe(2);
        cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        await cut.Find("[data-testid='tenants-remove-member-flow'] form").SubmitAsync();
        deliveries.Count.ShouldBe(2);
        deliveries[1].ShouldBe(deliveries[0]);
        acquisitions.ShouldBe(3);
    }

    [Fact]
    public void Member_access_review_retargeting_creates_fresh_change_and_remove_flow_instances()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, detail)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1")
            .Add(p => p.Members, MemberSnapshot(detail)));

        cut.FindAll("[data-testid='tenants-change-role-open']")[0].Click();
        ChangeTenantMemberRoleFlow firstChange = cut.FindComponent<ChangeTenantMemberRoleFlow>().Instance;
        firstChange.Member.UserId.ShouldBe("owner-user");
        cut.FindAll("[data-testid='tenants-change-role-open']")[1].Click();
        ChangeTenantMemberRoleFlow secondChange = cut.FindComponent<ChangeTenantMemberRoleFlow>().Instance;
        secondChange.Member.UserId.ShouldBe("reader-user");
        secondChange.ShouldNotBeSameAs(firstChange);

        cut.FindAll("[data-testid='tenants-remove-member-open']")[0].Click();
        RemoveTenantMemberFlow firstRemove = cut.FindComponent<RemoveTenantMemberFlow>().Instance;
        firstRemove.Member.UserId.ShouldBe("owner-user");
        cut.FindAll("[data-testid='tenants-remove-member-open']")[1].Click();
        RemoveTenantMemberFlow secondRemove = cut.FindComponent<RemoveTenantMemberFlow>().Instance;
        secondRemove.Member.UserId.ShouldBe("reader-user");
        secondRemove.ShouldNotBeSameAs(firstRemove);
    }

    [Fact]
    public async Task Member_access_review_reserves_child_lease_before_delayed_parent_and_rejects_queued_switches()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        var parentEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var parentCompletion = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int parentAcquisitions = 0;
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, detail)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1")
            .Add(p => p.Members, MemberSnapshot(detail))
            .Add(p => p.CommandActivityLease, active =>
            {
                if (!active)
                {
                    return Task.FromResult(true);
                }

                Interlocked.Increment(ref parentAcquisitions);
                parentEntered.TrySetResult();
                return parentCompletion.Task;
            }));

        cut.FindAll("[data-testid='tenants-change-role-open']")[0].Click();
        ChangeTenantMemberRoleFlow changeFlow = cut.FindComponent<ChangeTenantMemberRoleFlow>().Instance;
        // CommandActivityLease reserves _childCommandLeaseOwner and calls the CommandActivityLease delegate
        // above synchronously, before it ever awaits the still-pending parentCompletion.Task -- so
        // parentEntered is already signalled by the time this line runs. The WaitAsync here is a deadlock
        // backstop against a future regression re-introducing a real yield point, not the primary
        // synchronization for this test.
        Task<bool> firstAcquisition = changeFlow.CommandActivityLease!(true);
        await parentEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        bool siblingAcquired = await cut.FindComponent<AddTenantMemberFlow>().Instance.CommandActivityLease!(true);
        siblingAcquired.ShouldBeFalse();
        parentAcquisitions.ShouldBe(1);

        // While the lease is held, every row's change-role/remove-member launcher renders as unavailable
        // (fail-closed at the rendering layer) rather than staying clickable-but-silently-refused, so there
        // is no button left to click that could queue a switch to a different member.
        cut.Render();
        cut.FindAll("[data-testid='tenants-remove-member-open']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-change-role-open']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-remove-member-flow']").ShouldBeEmpty();
        cut.FindComponent<ChangeTenantMemberRoleFlow>().Instance.ShouldBeSameAs(changeFlow);
        changeFlow.Member.UserId.ShouldBe("owner-user");

        parentCompletion.SetResult(true);
        (await firstAcquisition).ShouldBeTrue();
        (await changeFlow.CommandActivityLease(false)).ShouldBeTrue();
    }

    [Fact]
    public async Task Member_access_review_rolls_back_local_reservation_after_parent_refusal_or_exception()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        int acquisitionAttempt = 0;
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, detail)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1")
            .Add(p => p.Members, MemberSnapshot(detail))
            .Add(p => p.CommandActivityLease, active => !active
                ? Task.FromResult(true)
                : Interlocked.Increment(ref acquisitionAttempt) switch
                {
                    1 => Task.FromResult(false),
                    2 => Task.FromException<bool>(new InvalidOperationException("Parent lease failed.")),
                    _ => Task.FromResult(true),
                }));
        cut.FindAll("[data-testid='tenants-change-role-open']")[0].Click();
        AddTenantMemberFlow addFlow = cut.FindComponent<AddTenantMemberFlow>().Instance;
        ChangeTenantMemberRoleFlow changeFlow = cut.FindComponent<ChangeTenantMemberRoleFlow>().Instance;

        (await addFlow.CommandActivityLease!(true)).ShouldBeFalse();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => changeFlow.CommandActivityLease!(true));
        (await addFlow.CommandActivityLease(true)).ShouldBeTrue();
        (await addFlow.CommandActivityLease(false)).ShouldBeTrue();
    }

    [Fact]
    public async Task Member_access_review_clears_child_lease_owner_when_release_delegate_throws()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, detail)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1")
            .Add(p => p.Members, MemberSnapshot(detail))
            .Add(p => p.CommandActivityLease, active => active
                ? Task.FromResult(true)
                : Task.FromException<bool>(new InvalidOperationException("Release failed."))));
        cut.FindAll("[data-testid='tenants-change-role-open']")[0].Click();
        AddTenantMemberFlow addFlow = cut.FindComponent<AddTenantMemberFlow>().Instance;
        ChangeTenantMemberRoleFlow changeFlow = cut.FindComponent<ChangeTenantMemberRoleFlow>().Instance;

        (await changeFlow.CommandActivityLease!(true)).ShouldBeTrue();
        _ = await Should.ThrowAsync<InvalidOperationException>(() => changeFlow.CommandActivityLease!(false));

        // The release delegate throwing still clears the owner (via the finally in
        // HandleCommandActivityLeaseAsync), so a different row's flow can acquire the lease afterward.
        (await addFlow.CommandActivityLease!(true)).ShouldBeTrue();
    }

    [Fact]
    public void Member_access_review_fails_closed_when_command_surface_is_unavailable_without_a_reason()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(p => p.Detail, detail)
            .Add(p => p.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(p => p.Freshness, ReadModelFreshnessState.Current)
            .Add(p => p.Lifecycle, ProjectionLifecycleState.Current)
            .Add(p => p.ProjectionVersion, "tenant-sequence:1")
            .Add(p => p.Members, MemberSnapshot(detail))
            .Add(p => p.IsCommandSurfaceAvailable, false)
            .Add(p => p.CommandSurfaceUnavailableReason, string.Empty));

        cut.FindAll("[data-testid='tenants-change-role-open']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-remove-member-open']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-member-unavailable-reason']")
            .ShouldAllBe(static reason => reason.TextContent.Contains("command", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Member_access_review_wires_complete_ga_standing_into_known_platform_friction()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        int readerIndex = detail.Members
            .Select((member, index) => (member.UserId, index))
            .First(pair => string.Equals(pair.UserId, "reader-user", StringComparison.Ordinal))
            .index;
        GlobalAdministratorsSnapshot completeGa = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow(
                "reader-user",
                ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"ga\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = true,
        };

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail))
            .Add(view => view.GlobalAdministrators, completeGa));

        cut.FindAll("[data-testid='tenants-remove-member-open']")[readerIndex].Click();

        cut.Find("[data-testid='tenants-remove-member-platform-standing']").TextContent
            .ShouldContain("Also a global administrator");
        cut.Find("[data-testid='tenants-remove-member-global-admin-risk']").TextContent
            .ShouldContain("will not remove global-administrator authority");
    }

    [Fact]
    public void Member_access_review_keeps_current_positive_ga_standing_when_the_page_is_incomplete()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        int readerIndex = detail.Members
            .Select((member, index) => (member.UserId, index))
            .First(pair => string.Equals(pair.UserId, "reader-user", StringComparison.Ordinal))
            .index;
        GlobalAdministratorsSnapshot incompleteGa = GlobalAdministratorsSnapshot.Ready(
            [new GlobalAdministratorRow(
                "reader-user",
                ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current)],
            nextCursor: null,
            hasMore: false,
            eTag: "\"ga\"",
            freshness: ReadModelFreshnessState.Current) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            IsCompleteEvidence = false,
        };

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail))
            .Add(view => view.GlobalAdministrators, incompleteGa));

        cut.FindAll("[data-testid='tenants-remove-member-open']")[readerIndex].Click();

        cut.Find("[data-testid='tenants-remove-member-platform-standing']").TextContent
            .ShouldContain("Also a global administrator");
        cut.Find("[data-testid='tenants-remove-member-global-admin-risk']").TextContent
            .ShouldContain("will not remove global-administrator authority");
    }

    [Fact]
    public void Member_access_review_surfaces_all_canonical_unavailable_reason_categories_without_mutation_affordances()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));

        string[] categories =
        [
            "missing permission",
            "stale data",
            "missing lifecycle support",
            "missing consequence preview",
            "missing audit proof",
            "high-impact flow not ready",
        ];

        foreach (string category in categories)
        {
            cut.Find($"[data-testid='tenants-member-reason-{category.Replace(' ', '-')}']")
                .TextContent.ShouldContain(category);
        }

        cut.FindAll("[data-testid='tenants-member-action-slot']").Count.ShouldBeGreaterThanOrEqualTo(2);
        cut.FindAll("[data-testid='tenants-member-unavailable-reason']").Count.ShouldBe(2);
        cut.Find("[data-testid='tenants-add-member-flow']");
        cut.Find("[data-testid='tenants-add-member-submit']").GetAttribute("type").ShouldBe("submit");
        cut.FindAll("[data-testid='tenants-change-role-open']").Count.ShouldBe(2);
        cut.FindAll("[data-testid='tenants-remove-member-open']").Count.ShouldBe(2);
        cut.FindAll("[data-testid='tenants-member-action-slot']")
            .ShouldAllBe(static slot => slot.TextContent.Contains("Unavailable", StringComparison.OrdinalIgnoreCase));
        cut.Markup.ShouldNotContain("command payload", Case.Insensitive);
        cut.Markup.ShouldNotContain("accepted", Case.Insensitive);
        cut.Markup.ShouldNotContain("confirmed", Case.Insensitive);
    }

    /// <summary>
    /// Paging must not unmount an open member command flow.
    /// </summary>
    /// <remarks>
    /// ActiveChangeRoleMember and ActiveRemoveMember resolve against Members.Rows, so once the next page no
    /// longer contains the target the @if guard tears the flow component down mid-command and its lifecycle
    /// state, receipt and projection confirmation are destroyed with no announcement. The pager consulted
    /// only CanGoPrevious/HasMore/IsRefreshing and never the open-flow state.
    /// </remarks>
    [Fact]
    public void Member_paging_is_blocked_while_a_member_command_flow_is_open()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        TenantUsersSnapshot members = MemberSnapshot(detail) with { HasMore = true, NextCursor = "page-2" };

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, members));

        cut.Find("[data-testid='tenants-member-next']").HasAttribute("disabled").ShouldBeFalse();

        cut.Find("[data-testid='tenants-change-role-open']").Click();
        cut.Find("[data-testid='tenants-change-role-flow']");

        cut.Find("[data-testid='tenants-member-next']").HasAttribute("disabled").ShouldBeTrue(
            "paging away from an open change-role flow destroys its lifecycle state and receipt");
    }

    [Fact]
    public void Member_command_flow_closes_when_tenant_identity_changes_with_the_same_cursor()
    {
        RegisterComponentServices();
        TenantDetail alpha = Detail("tenant.alpha");
        TenantDetail beta = Detail("tenant.beta");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, alpha)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(alpha)));
        cut.FindAll("[data-testid='tenants-change-role-open']")[0].Click();
        cut.Find("[data-testid='tenants-change-role-flow']");

        cut.Render(parameters => parameters
            .Add(view => view.Detail, beta)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(beta)));

        cut.FindAll("[data-testid='tenants-change-role-flow']").ShouldBeEmpty();
    }

    [Fact]
    public void Member_access_review_opens_change_role_flow_without_removing_add_or_remove_action_slots()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));

        cut.Find("[data-testid='tenants-change-role-open']").Click();

        cut.Find("[data-testid='tenants-change-role-flow']");
        cut.Find("[data-testid='tenants-change-role-user-id']").TextContent.ShouldContain("owner-user");
        cut.Find("[data-testid='tenants-change-role-current-role']").TextContent.ShouldContain("Tenant owner");
        cut.Find("[data-testid='tenants-change-role-new-role']");
        cut.Find("[data-testid='tenants-change-role-submit']").GetAttribute("type").ShouldBe("submit");
        cut.Find("[data-testid='tenants-change-role-lifecycle']");
        cut.FindAll("[data-testid='tenants-member-action-slot']").Count.ShouldBe(2);
        cut.FindAll("[data-testid='tenants-remove-member-open']").Count.ShouldBe(2);
        cut.Find("[data-testid='tenants-add-member-flow']");
        cut.Find("[data-testid='tenants-add-member-submit']").GetAttribute("type").ShouldBe("submit");
        cut.Markup.ShouldContain("Remove member", Case.Insensitive);
    }

    [Theory]
    [InlineData(TenantStatus.Disabled, TenantDetailSurfaceKind.Ready, ReadModelFreshnessState.Current, "missing lifecycle support")]
    [InlineData(TenantStatus.Active, TenantDetailSurfaceKind.Stale, ReadModelFreshnessState.Stale, "stale data")]
    [InlineData(TenantStatus.Active, TenantDetailSurfaceKind.Ready, ReadModelFreshnessState.Unknown, "stale data")]
    [InlineData(TenantStatus.Active, TenantDetailSurfaceKind.Degraded, ReadModelFreshnessState.Current, "stale data")]
    [InlineData(TenantStatus.Active, TenantDetailSurfaceKind.Unavailable, ReadModelFreshnessState.Current, "stale data")]
    [InlineData(TenantStatus.Active, TenantDetailSurfaceKind.Unknown, ReadModelFreshnessState.Current, "stale data")]
    [InlineData(TenantStatus.Unknown, TenantDetailSurfaceKind.Ready, ReadModelFreshnessState.Current, "missing lifecycle support")]
    public void Member_access_review_fails_closed_for_disabled_stale_unknown_and_degraded_states(
        TenantStatus status,
        TenantDetailSurfaceKind surfaceKind,
        ReadModelFreshnessState freshness,
        string expectedReason)
    {
        RegisterComponentServices();
        TenantDetail detail = Detail(
                "tenant.alpha",
                new Dictionary<string, string>(),
                status,
                [new TenantMember("owner-user", TenantRole.TenantOwner)]);
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, surfaceKind)
            .Add(view => view.Freshness, freshness)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));

        cut.Find("[data-testid='tenants-member-section']").TextContent.ShouldContain(expectedReason);
        if (surfaceKind is TenantDetailSurfaceKind.Degraded or TenantDetailSurfaceKind.Unavailable or TenantDetailSurfaceKind.Unknown)
        {
            cut.FindAll("[data-testid='tenants-member-unavailable-reason']")
                .ShouldAllBe(static reason => !reason.TextContent.Contains("missing permission", StringComparison.OrdinalIgnoreCase));
        }

        cut.FindAll("[data-testid='tenants-member-action-slot']")
            .ShouldAllBe(static slot => slot.TextContent.Contains("Unavailable", StringComparison.OrdinalIgnoreCase));
        // Visible text only — avoids the Fluent success-color token false positive (see VisibleText).
        cut.VisibleText().ShouldNotContain("Success", Case.Insensitive);
    }

    [Fact]
    public void Member_access_review_true_authorization_failure_still_renders_missing_permission()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Unauthorized)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));

        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("missing permission");
        cut.FindAll("[data-testid='tenants-member-action-slot']")
            .ShouldAllBe(static slot => slot.TextContent.Contains("Unavailable", StringComparison.OrdinalIgnoreCase));
        cut.Markup.ShouldNotContain("accepted", Case.Insensitive);
        cut.Markup.ShouldNotContain("confirmed", Case.Insensitive);
    }

    [Fact]
    public void Member_access_review_does_not_render_backend_urls_tokens_payloads_or_command_lifecycle_context()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail(
                "tenant.alpha",
                new Dictionary<string, string>(),
                TenantStatus.Active,
                [new TenantMember("literal-user", TenantRole.TenantOwner)]);
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));

        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("literal-user");
        cut.Markup.ShouldNotContain("/api/tenants", Case.Insensitive);
        cut.Markup.ShouldNotContain("Bearer ", Case.Insensitive);
        cut.Markup.ShouldNotContain("jwt", Case.Insensitive);
        cut.Markup.ShouldNotContain("command payload", Case.Insensitive);
        cut.Markup.ShouldNotContain("correlation", Case.Insensitive);
        cut.Markup.ShouldNotContain("audit available", Case.Insensitive);
        cut.Markup.ShouldNotContain("preview available", Case.Insensitive);
    }

    [Fact]
    public void Member_access_review_renders_authorization_safe_empty_state()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail(
                "tenant.empty",
                new Dictionary<string, string>(),
                TenantStatus.Active,
                []);
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Unknown)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));

        cut.Find("[data-testid='tenants-member-empty']").TextContent.ShouldContain("No visible members");
        cut.Find("[data-testid='tenants-member-empty']").TextContent.ShouldContain("does not reveal hidden memberships");
        cut.Find("[data-testid='tenants-member-empty']").TextContent.ShouldContain("stale data");
        cut.Markup.ShouldNotContain("tenants-member-row");
    }

    // AC6: empty, unauthorized, not-found, error, degraded, stale and unknown remain distinct. The surface
    // previously routed every non-Empty kind through one "tenants-member-state" id and one
    // "Member read unavailable" title, so an authorization denial, a missing tenant, an expired cursor and a
    // failed read were indistinguishable to an operator and to any selector-based test.
    [Theory]
    [InlineData(TenantUsersSurfaceKind.Unauthorized, "tenants-member-unauthorized", "not authorized to view these members")]
    [InlineData(TenantUsersSurfaceKind.NotFound, "tenants-member-not-found", "member read was not found")]
    [InlineData(TenantUsersSurfaceKind.Invalid, "tenants-member-invalid", "no longer valid")]
    [InlineData(TenantUsersSurfaceKind.Unavailable, "tenants-member-unavailable", "cannot be loaded right now")]
    [InlineData(TenantUsersSurfaceKind.Error, "tenants-member-error", "member read failed")]
    [InlineData(TenantUsersSurfaceKind.Loading, "tenants-member-loading", "Loading visible members")]
    public void Member_access_review_renders_each_non_empty_read_state_distinctly(
        TenantUsersSurfaceKind kind,
        string expectedTestId,
        string expectedMessageFragment)
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");

        // Built through the production factories, not `MemberSnapshot(detail) with { Kind = kind }`. Every
        // terminal read state routes through TenantUsersSnapshot.EmptyState, which forces ETag,
        // ProjectionVersion, Freshness and Lifecycle to their no-evidence values. The `with` form kept
        // eTag "members-etag", projectionVersion "tenant-sequence:1" and Current/Current, so ActionsAreEvidenceBacked
        // evaluated TRUE against evidence no producer can emit and no regression in the evidence gate for a
        // terminal read state was catchable here.
        TenantUsersSnapshot members = TerminalMemberSnapshot(kind, detail.TenantId);

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, members));

        cut.Find($"[data-testid='{expectedTestId}']").TextContent.ShouldContain(expectedMessageFragment);

        // Never the authorization-safe absence id, which claims the tenant genuinely has no visible members.
        cut.FindAll("[data-testid='tenants-member-empty']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("tenants-member-row");

        // The member evidence gate must be closed for every one of these states: the detail read is fully
        // current, so only the member read can close it, and it can only do so because the fixture no longer
        // carries invented member evidence.
        members.ETag.ShouldBeNull();
        members.ProjectionVersion.ShouldBeNull();
        members.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
        members.Lifecycle.ShouldBe(ProjectionLifecycleState.Unknown);
        cut.Find("[data-testid='tenants-add-member-unavailable-reason']");
    }

    /// <summary>
    /// Builds a terminal member read state the way production does.
    /// </summary>
    private static TenantUsersSnapshot TerminalMemberSnapshot(TenantUsersSurfaceKind kind, string tenantId)
        => kind switch
        {
            TenantUsersSurfaceKind.Unauthorized => TenantUsersSnapshot.Unauthorized(tenantId),
            TenantUsersSurfaceKind.NotFound => TenantUsersSnapshot.NotFound(tenantId),
            TenantUsersSurfaceKind.Invalid => TenantUsersSnapshot.Invalid(tenantId),
            TenantUsersSurfaceKind.Unavailable => TenantUsersSnapshot.Unavailable(tenantId),
            TenantUsersSurfaceKind.Error => TenantUsersSnapshot.Error(tenantId),
            TenantUsersSurfaceKind.Loading => TenantUsersSnapshot.Loading(tenantId),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Not a terminal member read state."),
        };

    // The theory above omits Degraded, Stale and Unknown, so the three switch arms added for them could be
    // collapsed into the `_ => "tenants-member-state"` default with the suite green -- restoring exactly the
    // indistinguishable-state defect AC6 forbids. A repo-wide grep for these three ids returned a single
    // ShouldBeEmpty() negative before this test existed. Degraded and Stale share the generic title, so the
    // id is the only discriminator and is what this pins.
    [Theory]
    [InlineData(TenantUsersSurfaceKind.Degraded, "tenants-member-degraded")]
    [InlineData(TenantUsersSurfaceKind.Stale, "tenants-member-stale")]
    [InlineData(TenantUsersSurfaceKind.Unknown, "tenants-member-unknown")]
    public void Member_access_review_gives_degraded_stale_and_unknown_their_own_state_ids(
        TenantUsersSurfaceKind kind,
        string expectedTestId)
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");

        // Degraded comes straight from its factory. Stale and Unknown reach a row-less surface through the
        // 304 branch of MapTenantUsersResponse, which re-kinds a retained row-less snapshot from the
        // response freshness -- so the shape below (no rows, no ETag, no projection version, freshness and
        // lifecycle agreeing) is what the producer can actually deliver. The previous
        // `MemberSnapshot(detail) with { Kind = kind }` form carried live ETag/version/Current evidence that
        // no factory emits for these kinds.
        TenantUsersSnapshot members = kind switch
        {
            TenantUsersSurfaceKind.Degraded => TenantUsersSnapshot.Degraded(
                detail.TenantId,
                previous: null,
                TenantUsersReason.GatewayFailure) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") },
            TenantUsersSurfaceKind.Stale => TenantUsersSnapshot.Ready(
                detail.TenantId,
                [],
                nextCursor: null,
                hasMore: false,
                eTag: null,
                projectionVersion: null,
                ReadModelFreshnessState.Stale,
                ProjectionLifecycleState.Stale) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") },
            _ => TenantUsersSnapshot.Ready(
                detail.TenantId,
                [],
                nextCursor: null,
                hasMore: false,
                eTag: null,
                projectionVersion: null,
                ReadModelFreshnessState.Unknown,
                ProjectionLifecycleState.Unknown) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") },
        };
        members.Kind.ShouldBe(kind);
        members.IsAuthorizationScopedEmpty.ShouldBeFalse();

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, members));

        cut.Find($"[data-testid='{expectedTestId}']");
        cut.FindAll("[data-testid='tenants-member-state']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-member-empty']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("tenants-member-row");
    }

    // The shared MemberSnapshot fixture is built FROM detail.Members, so every test using it would still pass
    // if Rows, ActiveChangeRoleMember and ActiveRemoveMember were reverted to the pre-1.10 Detail.Members
    // source -- the exact defect this story exists to fix. This fixture is deliberately disjoint so the
    // authoritative read is the only thing that can produce the rendered rows and the flow targets.
    [Fact]
    public void Member_rows_and_command_flow_targets_come_only_from_the_authoritative_member_read()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail(
            "tenant.alpha",
            new Dictionary<string, string>(),
            TenantStatus.Active,
            [new TenantMember("user.embedded", TenantRole.TenantOwner)]);
        TenantUsersSnapshot authoritative = TenantUsersSnapshot.Ready(
            "tenant.alpha",
            [new TenantMember("user.authoritative", TenantRole.TenantOwner)],
            nextCursor: null,
            hasMore: false,
            eTag: "members-etag",
            projectionVersion: "tenant-sequence:1",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, authoritative));

        cut.FindAll("[data-testid='tenants-member-row']").Count.ShouldBe(1);
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("user.authoritative");

        // The embedded detail member must not reach any rendered surface, including the flow targets.
        cut.Markup.ShouldNotContain("user.embedded");
    }

    [Fact]
    public void Member_access_review_announces_page_one_recovery_after_an_expired_cursor()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        TenantUsersSnapshot members = MemberSnapshot(detail) with
        {
            PagingRecovered = true,
            Reason = TenantUsersReason.ListRefreshed,
        };

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, members));

        // epic-1-context: invalid or stale cursor state restarts at page 1 with an honest localized notice.
        IElement notice = cut.Find("[data-testid='tenants-member-page-recovered']");
        notice.GetAttribute("aria-live").ShouldBe("polite");
        notice.TextContent.ShouldContain("restarted at the first page");
    }

    // The owner context is computed from Detail.Members, which is a different read at a possibly different
    // projection version than the authoritative member page. When the two disagree it must not be rendered
    // as fact. This guard previously had no assertion anywhere in the suite.
    [Fact]
    public void Member_access_review_withholds_owner_context_when_member_and_detail_evidence_disagree()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        TenantUsersSnapshot mismatched = MemberSnapshot(detail) with { ProjectionVersion = "members-v2" };

        IRenderedComponent<MemberAccessReview> mismatch = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "detail-v1")
            .Add(view => view.Members, mismatched));

        string withheld = mismatch.Find("[data-testid='tenants-member-owner-context']").TextContent;
        withheld.ShouldContain("unavailable");
        withheld.ShouldNotContain("visible owner");

        IRenderedComponent<MemberAccessReview> agreeing = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));

        agreeing.Find("[data-testid='tenants-member-owner-context']").TextContent
            .ShouldNotContain("unavailable");
    }

    [Fact]
    public void Member_access_review_keeps_rows_readable_but_actions_unavailable_for_version_mismatch()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        TenantUsersSnapshot members = MemberSnapshot(detail) with { ProjectionVersion = "members-v2" };

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "detail-v1")
            .Add(view => view.Members, members));

        cut.FindAll("[data-testid='tenants-member-row']").Count.ShouldBe(detail.Members.Count);
        cut.FindAll("[data-testid='tenants-change-role-open']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-remove-member-open']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("stale data");
    }

    [Fact]
    public void Detail_member_rows_and_paging_use_only_the_dedicated_tenant_users_snapshot_and_exact_cursor_history()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        List<TenantUsersRequest> memberRequests = [];
        TenantDetail embeddedDetail = Detail(
            "tenant.alpha",
            new Dictionary<string, string>(),
            TenantStatus.Active,
            [new TenantMember("embedded-should-not-render", TenantRole.TenantOwner)]);
        TenantDetailSnapshot detailSnapshot = ReadyWithSafeConfiguration(
            embeddedDetail,
            ProjectionLifecycleState.Current,
            "projection-v1");
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(detailSnapshot));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantUsersRequest request = call.Arg<TenantUsersRequest>()
                    ?? throw new InvalidOperationException("A tenant-users request is required.");
                memberRequests.Add(request);
                return Task.FromResult(request.Cursor switch
                {
                    "cursor-page-2" => TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-2-user", TenantRole.TenantReader)],
                        nextCursor: null,
                        hasMore: false,
                        eTag: "members-page-2",
                        projectionVersion: "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") },
                    _ => TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-1-user", TenantRole.TenantOwner)],
                        nextCursor: "cursor-page-2",
                        hasMore: true,
                        eTag: "members-page-1",
                        projectionVersion: "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") },
                });
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-next']");

        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("page-1-user");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldNotContain("embedded-should-not-render");
        cut.Find("[data-testid='tenants-member-next']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("page-2-user"));
        cut.Find("[data-testid='tenants-member-previous']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("page-1-user"));

        memberRequests.Select(static request => request.Cursor).ShouldBe([null, "cursor-page-2", null]);
        memberRequests[1].ETag.ShouldBeNull();
        memberRequests[2].ETag.ShouldBeNull();
    }

    [Fact]
    public void Invalid_member_cursor_recovers_page_one_and_clears_failed_history()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        List<TenantUsersRequest> memberRequests = [];
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantUsersRequest request = call.Arg<TenantUsersRequest>()
                    ?? throw new InvalidOperationException("A tenant-users request is required.");
                memberRequests.Add(request);
                return Task.FromResult(memberRequests.Count switch
                {
                    1 => TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-1-before", TenantRole.TenantOwner)],
                        nextCursor: "expired-page-2",
                        hasMore: true,
                        eTag: "members-page-1-before",
                        projectionVersion: "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") },
                    2 => TenantUsersSnapshot.Invalid("tenant.alpha", TenantUsersReason.InvalidCursor),
                    3 => TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-1-recovered", TenantRole.TenantOwner)],
                        nextCursor: null,
                        hasMore: false,
                        eTag: "members-page-1-recovered",
                        projectionVersion: "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") },
                    _ => throw new InvalidOperationException("Unexpected tenant-users request."),
                });
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-next']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-member-table']")
            .TextContent.ShouldContain("page-1-recovered"));
        memberRequests.Select(static request => request.Cursor).ShouldBe([null, "expired-page-2", null]);
        cut.FindAll("[data-testid='tenants-member-previous']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("page-1-before");
    }

    [Fact]
    public void Member_paging_authorization_failure_discards_previously_visible_rows()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        int memberReads = 0;
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(Interlocked.Increment(ref memberReads) == 1
                ? TenantUsersSnapshot.Ready(
                    "tenant.alpha",
                    [new TenantMember("must-not-remain-visible", TenantRole.TenantOwner)],
                    "page-2",
                    true,
                    "members-page-1",
                    "projection-v1",
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") }
                : TenantUsersSnapshot.Unauthorized("tenant.alpha")));
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-next']").Click();

        cut.WaitForElement("[data-testid='tenants-member-unauthorized']");
        cut.Markup.ShouldNotContain("must-not-remain-visible");
    }

    [Fact]
    public async Task Notification_refresh_recovers_an_expired_member_cursor_at_page_one()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backendSubscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        backendSubscription
            .SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        List<TenantUsersRequest> memberRequests = [];
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantUsersRequest request = call.Arg<TenantUsersRequest>()!;
                memberRequests.Add(request);
                return Task.FromResult(memberRequests.Count switch
                {
                    1 => TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-1-before", TenantRole.TenantOwner)],
                        "page-2",
                        true,
                        "members-1",
                        "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") },
                    2 => TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-2-before", TenantRole.TenantReader)],
                        null,
                        false,
                        "members-2",
                        "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") },
                    3 => TenantUsersSnapshot.Invalid("tenant.alpha", TenantUsersReason.InvalidCursor),
                    4 => TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-1-recovered", TenantRole.TenantOwner)],
                        null,
                        false,
                        "members-recovered",
                        "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") },
                    _ => throw new InvalidOperationException("Unexpected member read."),
                });
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton(backendSubscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-next']").Click();
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("page-2-before"));
        await backendSubscription.Received(1)
            .SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>());

        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.alpha");

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-member-table']")
            .TextContent.ShouldContain("page-1-recovered"));
        memberRequests.Select(static request => request.Cursor).ShouldBe([null, "page-2", "page-2", null]);
        cut.FindAll("[data-testid='tenants-member-previous']").ShouldBeEmpty();
    }

    /// <summary>
    /// A page-one recovery performed inside the gateway must reset the page's own cursor and history.
    /// </summary>
    /// <remarks>
    /// The gateway recovers an expired member cursor itself and returns page one. The page branched only on
    /// an explicit <c>InvalidCursor</c> reason, which a recovered read never carries, so it treated the
    /// answer as the requested page: it committed the expired cursor, pushed a history entry -- enabling
    /// Previous on a page-one view -- and re-sent the dead cursor on every later refresh. Three mutations
    /// survived the suite before this test existed.
    /// </remarks>
    [Fact]
    public async Task Gateway_side_member_page_recovery_resets_the_page_cursor_and_history()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backendSubscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        backendSubscription
            .SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);
        List<TenantUsersRequest> memberRequests = [];
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantUsersRequest request = call.Arg<TenantUsersRequest>()!;
                memberRequests.Add(request);
                return Task.FromResult(memberRequests.Count == 1
                    ? TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-1-user", TenantRole.TenantOwner)],
                        "page-2",
                        true,
                        "members-1",
                        "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") }

                    // The shape the gateway produces after recovering internally: page one rows, no request
                    // cursor, ListRefreshed, PagingRecovered. Never an InvalidCursor reason.
                    : TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-1-recovered", TenantRole.TenantOwner)],
                        null,
                        false,
                        "members-recovered",
                        "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with
                    {
                        CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                        Reason = TenantUsersReason.ListRefreshed,
                        PagingRecovered = true,
                    });
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton(backendSubscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-next']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-member-table']")
            .TextContent.ShouldContain("page-1-recovered"));

        // The click really did ask for the now-dead cursor: the reset comes from the response, not the request.
        memberRequests[1].Cursor.ShouldBe("page-2");

        // The operator is told the list restarted...
        cut.Find("[data-testid='tenants-member-page-recovered']");

        // ...Previous is gone, because this is page one and the history was cleared...
        cut.FindAll("[data-testid='tenants-member-previous']").ShouldBeEmpty();

        // ...and a later notification refresh re-reads page one, not the dead cursor.
        await backendSubscription.Received(1)
            .SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>());
        notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.alpha");
        cut.WaitForAssertion(() => memberRequests.Count.ShouldBe(3));
        memberRequests[2].Cursor.ShouldBeNull();
    }

    [Fact]
    public async Task Repeated_member_next_click_while_loading_starts_only_one_page_read()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        List<TenantUsersRequest> memberRequests = [];
        var pendingPage = new TaskCompletionSource<TenantUsersSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantUsersRequest request = call.Arg<TenantUsersRequest>()
                    ?? throw new InvalidOperationException("A tenant-users request is required.");
                memberRequests.Add(request);
                return memberRequests.Count == 1
                    ? Task.FromResult(TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-1-user", TenantRole.TenantOwner)],
                        nextCursor: "cursor-page-2",
                        hasMore: true,
                        eTag: "members-page-1",
                        projectionVersion: "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") })
                    : pendingPage.Task;
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        IElement next = cut.WaitForElement("[data-testid='tenants-member-next']");

        Task firstClick = next.ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        cut.WaitForAssertion(() =>
        {
            memberRequests.Count.ShouldBe(2);
            cut.Find("[data-testid='tenants-member-next']").HasAttribute("disabled").ShouldBeTrue();
        });
        await cut.Find("[data-testid='tenants-member-next']")
            .ClickAsync(new Microsoft.AspNetCore.Components.Web.MouseEventArgs());
        memberRequests.Count.ShouldBe(2);

        pendingPage.SetResult(TenantUsersSnapshot.Ready(
            "tenant.alpha",
            [new TenantMember("page-2-user", TenantRole.TenantReader)],
            nextCursor: null,
            hasMore: false,
            eTag: "members-page-2",
            projectionVersion: "projection-v1",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") });
        await firstClick;

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-member-table']")
            .TextContent.ShouldContain("page-2-user"));
        memberRequests.Select(static request => request.Cursor).ShouldBe([null, "cursor-page-2"]);
    }

    [Fact]
    public void Workspace_detail_link_preserves_non_cursor_context_in_return_url()
    {
        TenantListSnapshot snapshot = TenantListSnapshot.Ready(
            [
                TenantListRow.FromSummary(new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)),
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current,
            isDegraded: false);
        RegisterServices(snapshot);

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants?search=alpha&status=Active&sort=name&desc=True&cursor=cursor-1");

        IRenderedComponent<TenantsWorkspace> cut = Render<TenantsWorkspace>();
        cut.WaitForElement("[data-testid='tenants-list-detail-link']");

        string href = cut.Find("[data-testid='tenants-list-detail-link']").GetAttribute("href").ShouldNotBeNull();
        href.ShouldStartWith("/tenants/tenant.alpha?returnUrl=");
        string decoded = Uri.UnescapeDataString(href[(href.IndexOf("returnUrl=", StringComparison.Ordinal) + "returnUrl=".Length)..]);
        decoded.ShouldContain("search=alpha");
        decoded.ShouldContain("status=Active");
        decoded.ShouldContain("sort=name");
        decoded.ShouldContain("desc=True");
        decoded.ShouldNotContain("cursor=cursor-1");
        decoded.ShouldContain("selected=tenant.alpha");
        decoded.ShouldContain("anchor=tenant-row-id-dGVuYW50LmFscGhh");
    }

    [Fact]
    public void Workspace_restores_return_context_from_query_before_loading_list()
    {
        List<TenantListRequest> requests = [];
        TenantListSnapshot snapshot = TenantListSnapshot.Ready(
            [
                TenantListRow.FromSummary(new TenantSummary("tenant.alpha", "Alpha", TenantStatus.Active)),
                TenantListRow.FromSummary(new TenantSummary("tenant.beta", "Beta", TenantStatus.Disabled)),
            ],
            nextCursor: null,
            hasMore: false,
            eTag: "\"etag\"",
            freshness: ReadModelFreshnessState.Current,
            isDegraded: false);
        RegisterListServices(call =>
        {
            requests.Add(call.ArgAt<TenantListRequest>(0));
            return Task.FromResult(snapshot);
        });

        Services.GetRequiredService<NavigationManager>()
            .NavigateTo("/tenants?search=beta&status=Disabled&sort=name&desc=True&cursor=cursor-1&selected=tenant.beta&anchor=tenant-row-id-dGVuYW50LmJldGE");

        IRenderedComponent<TenantsWorkspace> cut = Render<TenantsWorkspace>();
        cut.WaitForElement("[data-testid='tenants-list-return-context']");

        TenantListRequest request = requests.ShouldHaveSingleItem();
        request.Search.ShouldBe("beta");
        request.Status.ShouldBe(TenantStatus.Disabled);
        request.SortColumn.ShouldBe(TenantListSortColumns.Name);
        request.SortDescending.ShouldBeTrue();
        request.Cursor.ShouldBeNull();
        cut.Find("[data-testid='tenants-list-return-context']").TextContent.ShouldContain("tenant.beta");
        cut.Markup.ShouldContain("tenant.beta");
        cut.Markup.ShouldNotContain("tenant.alpha");

        // The generated return anchor (anchor=tenant-row-id-dGVuYW50LmJldGE) must resolve to a real DOM
        // target so focus/scroll restoration has something to land on.
        cut.Find("[id='tenant-row-id-dGVuYW50LmJldGE']").ShouldNotBeNull();
        cut.Find("[id='tenants-list-heading']").GetAttribute("tabindex").ShouldBe("-1");
    }

    [Fact]
    public void Detail_styles_preserve_responsive_safety_and_forced_colors_hooks()
    {
        string projectRoot = ProjectRoot();
        string styles = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Pages",
            "TenantDetailPage.razor.css"));

        styles.ShouldContain("overflow-wrap: anywhere");
        styles.ShouldContain("white-space: break-spaces");
        styles.ShouldContain("grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr))");
        styles.ShouldContain("@media (max-width: 767px)");
        styles.ShouldContain("grid-template-columns: minmax(0, 1fr)");
        styles.ShouldContain("@media (forced-colors: active)");
        styles.ShouldContain(":focus-visible");

        string configurationStyles = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "TenantConfigurationView.razor.css"));

        configurationStyles.ShouldContain("overflow-wrap: anywhere");
        configurationStyles.ShouldContain("grid-template-columns");
        configurationStyles.ShouldContain("@media (max-width: 767px)");
        configurationStyles.ShouldContain("@media (forced-colors: active)");
        configurationStyles.ShouldContain(":focus-visible");

        string memberStyles = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Members",
            "MemberAccessReview.razor.css"));

        memberStyles.ShouldContain("overflow-wrap: anywhere");
        memberStyles.ShouldContain("white-space: break-spaces");
        memberStyles.ShouldContain("grid-template-columns");
        memberStyles.ShouldContain("min-width");
        memberStyles.ShouldContain("@media (max-width: 767px)");
        memberStyles.ShouldContain("@media (forced-colors: active)");
        memberStyles.ShouldContain(":focus-visible");
        memberStyles.ShouldContain("grid-template-columns: minmax(0, 1fr) auto");

        string lifecycleStyles = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Lifecycle",
            "TenantLifecycleActionAvailability.razor.css"));

        lifecycleStyles.ShouldContain("overflow-wrap: anywhere");
        lifecycleStyles.ShouldContain("grid-template-columns");
        lifecycleStyles.ShouldContain("@media (max-width: 767px)");
        lifecycleStyles.ShouldContain("@media (forced-colors: active)");
        lifecycleStyles.ShouldContain(":focus-visible");

        string changeRoleStyles = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Members",
            "ChangeTenantMemberRoleFlow.razor.css"));

        changeRoleStyles.ShouldContain("overflow-wrap: anywhere");
        changeRoleStyles.ShouldContain("grid-template-columns");
        changeRoleStyles.ShouldContain("@media (max-width: 767px)");
        changeRoleStyles.ShouldContain("@media (forced-colors: active)");
        changeRoleStyles.ShouldContain(":focus-visible");

        string addMemberStyles = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Tenants",
            "Members",
            "AddTenantMemberFlow.razor.css"));

        addMemberStyles.ShouldContain("overflow-wrap: anywhere");
        addMemberStyles.ShouldContain("grid-template-columns");
        addMemberStyles.ShouldContain("@media (max-width: 767px)");
        addMemberStyles.ShouldContain("@media (forced-colors: active)");
        addMemberStyles.ShouldContain(":focus-visible");

        string copyStyles = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Shared",
            "SupportSafeCopyButton.razor.css"));

        copyStyles.ShouldContain("inline-size");
        copyStyles.ShouldContain(":focus-visible");
        copyStyles.ShouldContain("@media (forced-colors: active)");
        copyStyles.ShouldContain("min-inline-size");
        copyStyles.ShouldNotContain("animation:", Case.Insensitive);
        copyStyles.ShouldNotContain("transition:", Case.Insensitive);
    }

    [Fact]
    public void French_resources_include_detail_and_configuration_keys()
    {
        string projectRoot = ProjectRoot();
        string invariantResources = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.resx"));
        string frenchResources = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.fr.resx"));

        frenchResources.ShouldContain("Tenants.Detail.Title");
        frenchResources.ShouldContain("Tenants.Detail.State.Unauthorized.Message");
        frenchResources.ShouldContain("Tenants.Detail.Configuration.Summary");
        invariantResources.ShouldContain("Tenants.Configuration.Title");
        invariantResources.ShouldContain("Tenants.Configuration.State.Unavailable");
        invariantResources.ShouldContain("Tenants.Configuration.State.Unavailable");
        invariantResources.ShouldContain("Tenants.Configuration.Management.Unavailable.NoScope");
        frenchResources.ShouldContain("Tenants.Configuration.Title");
        frenchResources.ShouldContain("Tenants.Configuration.State.Unavailable");
        frenchResources.ShouldContain("Tenants.Configuration.State.Unavailable");
        frenchResources.ShouldContain("Tenants.Configuration.Management.Unavailable.NoScope");
        invariantResources.ShouldContain("Tenants.Configuration.Set.Title");
        invariantResources.ShouldContain("Tenants.Configuration.Set.State.ProjectionPending");
        frenchResources.ShouldContain("Tenants.Configuration.Set.Title");
        frenchResources.ShouldContain("Tenants.Configuration.Set.State.ProjectionPending");
        invariantResources.ShouldContain("Tenants.Configuration.Remove.Title");
        invariantResources.ShouldContain("Tenants.Configuration.Remove.State.ProjectionPending");
        frenchResources.ShouldContain("Tenants.Configuration.Remove.Title");
        frenchResources.ShouldContain("Tenants.Configuration.Remove.State.ProjectionPending");
        invariantResources.ShouldContain("Tenants.Members.Title");
        invariantResources.ShouldContain("Tenants.Members.UnavailableReason.MissingPermission");
        frenchResources.ShouldContain("Tenants.Members.Title");
        frenchResources.ShouldContain("Tenants.Members.UnavailableReason.MissingPermission");
        invariantResources.ShouldContain("Tenants.Lifecycle.Title");
        invariantResources.ShouldContain("Tenants.Lifecycle.Unavailable.Governance");
        invariantResources.ShouldContain("Tenants.Lifecycle.Unavailable.AlreadyActive");
        frenchResources.ShouldContain("Tenants.Lifecycle.Title");
        frenchResources.ShouldContain("Tenants.Lifecycle.Unavailable.Governance");
        frenchResources.ShouldContain("Tenants.Lifecycle.Unavailable.AlreadyActive");
        invariantResources.ShouldContain("Tenants.AddMember.Title");
        invariantResources.ShouldContain("Tenants.AddMember.State.ProjectionPending");
        frenchResources.ShouldContain("Tenants.AddMember.Title");
        frenchResources.ShouldContain("Tenants.AddMember.State.ProjectionPending");
        invariantResources.ShouldContain("Tenants.ChangeRole.Title");
        invariantResources.ShouldContain("Tenants.ChangeRole.State.AlreadyApplied");
        frenchResources.ShouldContain("Tenants.ChangeRole.Title");
        frenchResources.ShouldContain("Tenants.ChangeRole.State.AlreadyApplied");
        invariantResources.ShouldContain("Tenants.RemoveMember.Title");
        invariantResources.ShouldContain("Tenants.RemoveMember.State.ProjectionPending");
        frenchResources.ShouldContain("Tenants.RemoveMember.Title");
        frenchResources.ShouldContain("Tenants.RemoveMember.State.ProjectionPending");
        invariantResources.ShouldContain("Tenants.Copy.Action");
        invariantResources.ShouldContain("Tenants.Copy.Feedback.Unsafe");
        frenchResources.ShouldContain("Tenants.Copy.Action");
        frenchResources.ShouldContain("Tenants.Copy.Feedback.Unsafe");
    }

    [Fact]
    public void Configuration_resources_have_full_invariant_and_french_parity()
    {
        string projectRoot = ProjectRoot();
        HashSet<string> invariantKeys = ConfigurationResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.resx"));
        HashSet<string> frenchKeys = ConfigurationResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.fr.resx"));

        invariantKeys.ShouldNotBeEmpty();
        frenchKeys.ShouldBe(invariantKeys, ignoreOrder: true);
    }

    [Fact]
    public void Member_resources_have_full_invariant_and_french_parity()
    {
        string projectRoot = ProjectRoot();
        HashSet<string> invariantKeys = MemberResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.resx"));
        HashSet<string> frenchKeys = MemberResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.fr.resx"));

        invariantKeys.ShouldNotBeEmpty();
        frenchKeys.ShouldBe(invariantKeys, ignoreOrder: true);
    }

    [Fact]
    public void Lifecycle_resources_have_full_invariant_and_french_parity()
    {
        string projectRoot = ProjectRoot();
        HashSet<string> invariantKeys = LifecycleResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.resx"));
        HashSet<string> frenchKeys = LifecycleResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.fr.resx"));

        invariantKeys.ShouldNotBeEmpty();
        frenchKeys.ShouldBe(invariantKeys, ignoreOrder: true);
    }

    [Fact]
    public void Copy_resources_have_full_invariant_and_french_parity()
    {
        string projectRoot = ProjectRoot();
        HashSet<string> invariantKeys = CopyResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.resx"));
        HashSet<string> frenchKeys = CopyResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.fr.resx"));

        invariantKeys.ShouldNotBeEmpty();
        frenchKeys.ShouldBe(invariantKeys, ignoreOrder: true);
    }

    [Fact]
    public void Add_member_resources_have_full_invariant_and_french_parity()
    {
        string projectRoot = ProjectRoot();
        HashSet<string> invariantKeys = AddMemberResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.resx"));
        HashSet<string> frenchKeys = AddMemberResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.fr.resx"));

        invariantKeys.ShouldNotBeEmpty();
        frenchKeys.ShouldBe(invariantKeys, ignoreOrder: true);
    }

    [Fact]
    public void Change_role_resources_have_full_invariant_and_french_parity()
    {
        string projectRoot = ProjectRoot();
        HashSet<string> invariantKeys = ChangeRoleResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.resx"));
        HashSet<string> frenchKeys = ChangeRoleResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.fr.resx"));

        invariantKeys.ShouldNotBeEmpty();
        frenchKeys.ShouldBe(invariantKeys, ignoreOrder: true);
    }

    [Fact]
    public void Remove_member_resources_have_full_invariant_and_french_parity()
    {
        string projectRoot = ProjectRoot();
        HashSet<string> invariantKeys = RemoveMemberResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.resx"));
        HashSet<string> frenchKeys = RemoveMemberResourceKeys(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Resources",
            "TenantsResources.fr.resx"));

        invariantKeys.ShouldNotBeEmpty();
        frenchKeys.ShouldBe(invariantKeys, ignoreOrder: true);
    }

    [Fact]
    public void Copy_source_uses_clipboard_module_without_browser_backend_or_legacy_fallbacks()
    {
        string projectRoot = ProjectRoot();
        string component = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "Components",
            "Shared",
            "SupportSafeCopyButton.razor"));
        string script = File.ReadAllText(Path.Combine(
            projectRoot,
            "src",
            "Hexalith.Tenants.UI",
            "wwwroot",
            "js",
            "tenantsClipboard.js"));

        component.ShouldContain("IJSRuntime");
        component.ShouldContain("JSDisconnectedException");
        component.ShouldNotContain("HttpClient");
        component.ShouldNotContain("ILogger");
        component.ShouldNotContain("Console.");
        component.ShouldNotContain("JsonSerializer");
        component.ShouldNotContain("localStorage", Case.Insensitive);
        component.ShouldNotContain("sessionStorage", Case.Insensitive);
        script.ShouldContain("navigator.clipboard.writeText");
        script.ShouldNotContain("document.execCommand", Case.Insensitive);
        script.ShouldNotContain("GET /api/", Case.Insensitive);
        script.ShouldNotContain("fetch(", Case.Insensitive);
        script.ShouldNotContain("XMLHttpRequest", Case.Insensitive);
        script.ShouldNotContain("sendBeacon", Case.Insensitive);
        script.ShouldNotContain("console.", Case.Insensitive);
        script.ShouldNotContain("JSON.stringify", Case.Insensitive);
        script.ShouldNotContain("access_token", Case.Insensitive);
        script.ShouldNotContain("localStorage", Case.Insensitive);
        script.ShouldNotContain("sessionStorage", Case.Insensitive);
    }

    private static HashSet<string> ConfigurationResourceKeys(string resourcePath)
        => Regex.Matches(File.ReadAllText(resourcePath), "name=\"(Tenants\\.Configuration[^\"]+)\"")
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> MemberResourceKeys(string resourcePath)
        => Regex.Matches(File.ReadAllText(resourcePath), "name=\"(Tenants\\.Members[^\"]+)\"")
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> LifecycleResourceKeys(string resourcePath)
        => Regex.Matches(File.ReadAllText(resourcePath), "name=\"(Tenants\\.Lifecycle[^\"]+)\"")
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> CopyResourceKeys(string resourcePath)
        => Regex.Matches(File.ReadAllText(resourcePath), "name=\"(Tenants\\.Copy[^\"]+)\"")
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> AddMemberResourceKeys(string resourcePath)
        => Regex.Matches(File.ReadAllText(resourcePath), "name=\"(Tenants\\.AddMember[^\"]+)\"")
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> ChangeRoleResourceKeys(string resourcePath)
        => Regex.Matches(File.ReadAllText(resourcePath), "name=\"(Tenants\\.ChangeRole[^\"]+)\"")
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    private static HashSet<string> RemoveMemberResourceKeys(string resourcePath)
        => Regex.Matches(File.ReadAllText(resourcePath), "name=\"(Tenants\\.RemoveMember[^\"]+)\"")
            .Select(static match => match.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

    [Theory]
    [InlineData("change", false)]
    [InlineData("change", true)]
    [InlineData("remove", false)]
    [InlineData("remove", true)]
    public void LandmarkReplacementRestoresRetainedLiteralTargetEvenWhenAbsentFromCurrentPage(string kind, bool absent)
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        TenantCommandAttemptTracker tracker = Services.GetRequiredService<TenantCommandAttemptTracker>();
        object original = kind is "change"
            ? TenantChangeRoleCommandSnapshot.Idle().RequestSent(new("tenant.alpha", "reader-user", TenantRole.TenantContributor), TenantRole.TenantReader, 1, "tenant-sequence:1")
                .Accepted(TenantCommandSubmissionResult.Accepted("original-id", "original-correlation"))
            : TenantRemoveMemberCommandSnapshot.Idle().Previewed(new("tenant.alpha", "reader-user"), TenantRole.TenantReader, 1, false, detail)
                .RequestSent("tenant-sequence:1").Accepted(TenantCommandSubmissionResult.Accepted("original-id", "original-correlation"));
        object owner = new();
        tracker.TryBegin("tenant.alpha", owner, original, out TenantRetainedCommandAttempt? attempt).ShouldBeTrue();
        tracker.TryStartDelivery(attempt!, owner).ShouldBeTrue();
        tracker.TryCompleteDelivery(attempt!, original, original).ShouldBeTrue();
        tracker.Detach(attempt, owner);
        IRenderedComponent<MemberAccessReview> RenderLandmark() => Render<MemberAccessReview>(p => p
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(x => x.Detail, detail).Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(x => x.Freshness, ReadModelFreshnessState.Current).Add(x => x.Lifecycle, ProjectionLifecycleState.Current)
            .Add(x => x.ProjectionVersion, "tenant-sequence:900").Add(x => x.AuditProofCapabilityAvailable, true)
            .Add(x => x.Members, absent ? MemberSnapshot(detail) with { Rows = [detail.Members[0]] } : MemberSnapshot(detail)));
        IRenderedComponent<MemberAccessReview> first = RenderLandmark();
        if (kind is "change")
        {
            ChangeTenantMemberRoleFlow child = first.FindComponent<ChangeTenantMemberRoleFlow>().Instance;
            tracker.IsAttached(attempt!, child).ShouldBeTrue();
            child.Dispose();
            tracker.IsAttached(attempt!, child).ShouldBeFalse();
        }
        else
        {
            RemoveTenantMemberFlow child = first.FindComponent<RemoveTenantMemberFlow>().Instance;
            tracker.IsAttached(attempt!, child).ShouldBeTrue();
            child.Dispose();
            tracker.IsAttached(attempt!, child).ShouldBeFalse();
        }
        first.Dispose();
        IRenderedComponent<MemberAccessReview> replacement = RenderLandmark();
        if (kind is "change")
        {
            replacement.FindComponent<ChangeTenantMemberRoleFlow>().Instance.Member.UserId.ShouldBe("reader-user");
            replacement.FindComponent<ChangeTenantMemberRoleFlow>().Instance.Snapshot.ShouldBe(original);
        }
        else
        {
            replacement.FindComponent<RemoveTenantMemberFlow>().Instance.Member.UserId.ShouldBe("reader-user");
            replacement.FindComponent<RemoveTenantMemberFlow>().Instance.Snapshot.ShouldBe(original);
        }
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LandmarkChildReplacementWaitsForItsOwnParentAdmissionAndOldCompletionCannotClearIt(bool oldApproved)
    {
        TrackingMembershipCommandGateway gateway = new(Task.FromResult(TenantCommandSubmissionResult.Accepted("message-1", "correlation-1")));
        RegisterComponentServices(gateway);
        var oldAdmission = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementAdmission = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        int acquisitions = 0;
        Func<bool, Task<bool>> lease = active => !active ? Task.FromResult(true)
            : Interlocked.Increment(ref acquisitions) == 1 ? oldAdmission.Task : replacementAdmission.Task;
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> landmark = Render<MemberAccessReview>(p => p
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(x => x.Detail, detail).Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(x => x.Freshness, ReadModelFreshnessState.Current).Add(x => x.Lifecycle, ProjectionLifecycleState.Current)
            .Add(x => x.ProjectionVersion, "tenant-sequence:1").Add(x => x.Members, MemberSnapshot(detail))
            .Add(x => x.CommandActivityLease, lease));
        landmark.FindAll("[data-testid='tenants-change-role-open']").Last().Click();
        IRenderedComponent<ChangeTenantMemberRoleFlow> first = landmark.FindComponent<ChangeTenantMemberRoleFlow>();
        FluentSelectInterop.ChangeFluentSelect(first, "tenants-change-role-new-role", nameof(TenantRole.TenantContributor));
        Task oldSubmit = first.Find("form").SubmitAsync();
        landmark.WaitForAssertion(() => acquisitions.ShouldBe(1));
        MethodInfo enter = typeof(MemberAccessReview).GetMethod("HandleChangeRoleCommandActivityLeaseAsync", BindingFlags.Instance | BindingFlags.NonPublic)!;
        (await landmark.InvokeAsync(() => (Task<bool>)enter.Invoke(landmark.Instance, [true])!)).ShouldBeFalse();
        gateway.ChangeRoleCallCount.ShouldBe(0);
        landmark.Render(p => p.Add(x => x.Members, MemberSnapshot(detail) with { RequestCursor = "replacement-page" }));
        landmark.FindAll("[data-testid='tenants-change-role-open']").Last().Click();
        IRenderedComponent<ChangeTenantMemberRoleFlow> replacement = landmark.FindComponent<ChangeTenantMemberRoleFlow>();
        FluentSelectInterop.ChangeFluentSelect(replacement, "tenants-change-role-new-role", nameof(TenantRole.TenantContributor));
        Task nextSubmit = replacement.Find("form").SubmitAsync();
        landmark.WaitForAssertion(() => acquisitions.ShouldBe(2));
        oldAdmission.SetResult(oldApproved);
        await oldSubmit.WaitAsync(TimeSpan.FromSeconds(5));
        gateway.ChangeRoleCallCount.ShouldBe(0);
        (await landmark.InvokeAsync(() => (Task<bool>)enter.Invoke(landmark.Instance, [true])!)).ShouldBeFalse();
        replacementAdmission.SetResult(true);
        await nextSubmit.WaitAsync(TimeSpan.FromSeconds(5));
        gateway.ChangeRoleCallCount.ShouldBe(1);
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeTrue();
    }

    [Fact]
    public async Task PageReplacementRendersSiblingAvailabilityWhenOwnerlessTerminalEvidenceReleasesAdmission()
    {
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:1")));
        var heldStatus = new TaskCompletionSource<TenantCommandStatusResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantCommandGateway gateway = Substitute.For<ITenantCommandGateway>();
        gateway.AddUserToTenantAsync(Arg.Any<AddUserToTenant>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(call.ArgAt<string>(1), "original-correlation")));
        gateway.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>()).Returns(heldStatus.Task);
        Services.AddSingleton(gateway);
        IRenderedComponent<TenantDetailPage> first = Render<TenantDetailPage>(p => p.Add(x => x.TenantId, "tenant.alpha"));
        first.WaitForElement("[data-testid='tenants-add-member-user-id']");
        IRenderedComponent<AddTenantMemberFlow> add = first.FindComponent<AddTenantMemberFlow>();
        add.Find("[data-testid='tenants-add-member-user-id']").Change("new-reader");
        FluentSelectInterop.ChangeFluentSelect(add, "tenants-add-member-role", nameof(TenantRole.TenantReader));
        Task submission = add.Find("form").SubmitAsync();
        first.WaitForAssertion(() => gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name is nameof(ITenantCommandGateway.GetStatusAsync)).ShouldBe(1));
        TenantCommandAttemptTracker tracker = Services.GetRequiredService<TenantCommandAttemptTracker>();
        TenantRetainedCommandAttempt retained = (TenantRetainedCommandAttempt)typeof(AddTenantMemberFlow)
            .GetField("_retainedAttempt", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(add.Instance)!;
        tracker.IsAttached(retained, add.Instance).ShouldBeTrue();
        add.Instance.Dispose();
        tracker.IsAttached(retained, add.Instance).ShouldBeFalse();
        first.Dispose();
        IRenderedComponent<TenantDetailPage> replacement = Render<TenantDetailPage>(p => p.Add(x => x.TenantId, "tenant.alpha"));
        replacement.WaitForAssertion(() => replacement.FindComponent<EditTenantMetadataFlow>().Instance.IsCommandSurfaceAvailable.ShouldBeFalse());
        heldStatus.SetResult(new(CommandStatus.Rejected, HasVerifiedCommandIdentity: true));
        await submission.WaitAsync(TimeSpan.FromSeconds(5));
        replacement.WaitForAssertion(() =>
        {
            replacement.FindComponent<AddTenantMemberFlow>().Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Rejected);
            replacement.FindComponent<EditTenantMetadataFlow>().Instance.IsCommandSurfaceAvailable.ShouldBeTrue();
            replacement.Find("[data-testid='tenants-edit-metadata-open']");
        });
        Services.GetRequiredService<TenantAggregateCommandAdmissionGate>().IsLocked(TenantCommandAggregateLock.ForTenant("tenant.alpha")).ShouldBeFalse();
    }

    private void RegisterComponentServices(ITenantCommandGateway? commandGateway = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(commandGateway ?? new StubTenantCommandGateway());
        Services.AddSingleton<ITenantQueryGateway>(Substitute.For<ITenantQueryGateway>());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        // The workspace only restores retained protected paging on an interactive render pass.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private static ITenantQueryGateway CreateCurrentDetailPageGateway()
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(
                Arg.Any<TenantDetailRequest>(),
                Arg.Any<TenantDetailSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string tenantId = call.ArgAt<TenantDetailRequest>(0).TenantId;
                return Task.FromResult(ReadyWithSafeConfiguration(
                    Detail(tenantId),
                    ProjectionLifecycleState.Current,
                    "tenant-sequence:1"));
            });
        gateway.GetTenantUsersAsync(
                Arg.Any<TenantUsersRequest>(),
                Arg.Any<TenantUsersSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                string tenantId = call.ArgAt<TenantUsersRequest>(0).TenantId;
                return Task.FromResult(MemberSnapshot(Detail(tenantId)) with
                {
                    ProjectionVersion = "tenant-sequence:1",
                });
            });
        return gateway;
    }

    private static void OpenReaderRemoval(IRenderedComponent<TenantDetailPage> cut, TenantDetail detail)
    {
        int readerIndex = detail.Members
            .Select((member, index) => (member.UserId, index))
            .First(pair => pair.UserId == "reader-user")
            .index;
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-remove-member-open']")
            .Count.ShouldBe(detail.Members.Count));
        cut.FindAll("[data-testid='tenants-remove-member-open']")[readerIndex].Click();
    }

    private void RegisterDetailPageServices(
        ITenantQueryGateway gateway,
        ITenantsBffComposition? bffComposition = null,
        bool configureAuthoritativeAuditCapability = true)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        if (configureAuthoritativeAuditCapability)
        {
            ConfigureAuthoritativeAuditCapability(gateway);
        }

        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton(bffComposition ?? new StubTenantsBffComposition());
        Services.AddFluentUIComponents();
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    [Theory]
    [InlineData("add")]
    [InlineData("role")]
    [InlineData("remove")]
    public async Task MemberProofReadsUnconditionallyAndRetainsFailedReadMetadata(string flow)
    {
        TenantDetail detail = Detail("tenant.alpha");
        TenantDetailSnapshot cached = ReadyWithSafeConfiguration(detail, projectionVersion: "tenant-sequence:42");
        RegisterServices(_ => Task.FromResult(cached));
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();
        TenantDetailSnapshot failure = TenantDetailSnapshot.Degraded(detail, "Proof refresh failed", "failed-validator",
            ProjectionLifecycleState.Current, "tenant-sequence:43").WithCallerScope(cached.CallerScope!);
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>()).Returns(failure);
        gateway.ClearReceivedCalls();
        (string method, object[] arguments) = flow switch
        {
            "add" => ("GetAddMemberProjectionEvidenceAsync", new object[] { new AddUserToTenant("tenant.alpha", "reader-user", TenantRole.TenantReader) }),
            "role" => ("GetChangeRoleProjectionEvidenceAsync", new object[] { new ChangeUserRole("tenant.alpha", "reader-user", TenantRole.TenantReader) }),
            _ => ("GetRemoveMemberProjectionEvidenceAsync", new object[] { new RemoveUserFromTenant("tenant.alpha", "reader-user") }),
        };
        object? result = await InvokeDetailProofAsync(cut, method, arguments);
        TenantDetailSnapshot returned = result.ShouldBeOfType<TenantDetailSnapshot>();
        returned.ShouldBeSameAs(failure);
        returned.Kind.ShouldBe(TenantDetailSurfaceKind.Degraded);
        returned.ProjectionVersion.ShouldBe("tenant-sequence:43");
        _ = gateway.Received(1).GetTenantAsync(Arg.Is<TenantDetailRequest>(request => request.ETag == null),
            Arg.Is<TenantDetailSnapshot?>(previous => previous == null), Arg.Any<CancellationToken>());
    }

    /// <summary>Fresh role evidence for the same principal withdraws stale owner/contributor admission.</summary>
    [Theory]
    [InlineData(TenantRole.TenantOwner, "add")]
    [InlineData(TenantRole.TenantContributor, "metadata")]
    public async Task ReviewParentFreshRoleChangeUsesRealBffAndRefusesStaleIntent(TenantRole initialRole, string kind)
    {
        TenantDetail initial = Detail("tenant.alpha", new Dictionary<string, string>(), TenantStatus.Active,
            [new TenantMember("operator.alpha", initialRole), new TenantMember("private-reader", TenantRole.TenantReader)]);
        TenantDetail current = initial;
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        RegisterReviewRealRoleParent(commands, _ => Task.FromResult(ReadyWithSafeConfiguration(current, ProjectionLifecycleState.Current, "tenant-sequence:1")));
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(x => x.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.FindComponent<EditTenantMetadataFlow>().Instance.IsAuthorized.ShouldBeTrue());
        if (kind is "metadata")
        {
            cut.Find("[data-testid='tenants-edit-metadata-open']").Click();
            cut.Find("[data-testid='tenants-edit-metadata-name']").Change("Stale contributor intent");
        }
        else
        {
            IRenderedComponent<AddTenantMemberFlow> add = cut.FindComponent<AddTenantMemberFlow>();
            add.Find("[data-testid='tenants-add-member-user-id']").Change("new-member");
            FluentSelectInterop.ChangeFluentSelect(add, "tenants-add-member-role", nameof(TenantRole.TenantReader));
        }
        current = initial with { Members = [new TenantMember("operator.alpha", TenantRole.TenantReader), new TenantMember("private-reader", TenantRole.TenantReader)] };
        if (kind is "metadata") await cut.FindComponent<EditTenantMetadataFlow>().Find("form").SubmitAsync();
        else await cut.FindComponent<AddTenantMemberFlow>().Find("form").SubmitAsync();
        commands.ReceivedCalls().ShouldBeEmpty();
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
        cut.WaitForAssertion(() =>
        {
            cut.FindComponent<EditTenantMetadataFlow>().Instance.AuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.MissingPermission);
            MemberAccessReview review = cut.FindComponent<MemberAccessReview>().Instance;
            review.AuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.MissingPermission);
            review.IsAddMemberAuthorized.ShouldBeFalse();
            review.IsChangeRoleAuthorized.ShouldBeFalse();
            review.IsRemoveMemberAuthorized.ShouldBeFalse();
        });
        cut.FindAll("[data-testid='tenants-edit-metadata-open']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-change-role-open']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-remove-member-open']").ShouldBeEmpty();
    }

    /// <summary>Fresh status/projection/preview changes invalidate admission without erasing older qualified proof.</summary>
    [Theory]
    [InlineData("disabled")]
    [InlineData("projection")]
    [InlineData("metadata")]
    public async Task ReviewParentChangedAdmissionEvidencePreservesEarlierProof(string change)
    {
        TenantDetail initial = Detail("tenant.alpha", new Dictionary<string, string>(), TenantStatus.Active,
            [new TenantMember("operator.alpha", TenantRole.TenantOwner), new TenantMember("reader", TenantRole.TenantReader)]);
        TenantDetail current = initial;
        string version = "tenant-sequence:2";
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        RegisterReviewRealRoleParent(commands, _ => Task.FromResult(ReadyWithSafeConfiguration(current, ProjectionLifecycleState.Current, version)));
        TenantDetail before = initial with { Name = "Before proven update" };
        TenantDetailSnapshot proof = TenantCommandProofFixtures.Proof(initial, "tenant-sequence:2")!.WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha"));
        TenantUpdateMetadataCommandSnapshot sent = TenantUpdateMetadataCommandSnapshot.Idle(before.Name, before.Description, before)
            .RequestSent(new UpdateTenant(initial.TenantId, initial.Name, initial.Description), "tenant-sequence:1");
        TenantUpdateMetadataCommandSnapshot previous = sent
            .Accepted(TenantCommandSubmissionResult.Accepted("previous-message", "previous-correlation"))
            .ApplyStatus(new(CommandStatus.Completed, EventCount: 1, HasVerifiedCommandIdentity: true) { CommittedEventSequence = 2 })
            .ConfirmProjection(proof);
        previous.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
        TenantCommandAttemptTracker tracker = Services.GetRequiredService<TenantCommandAttemptTracker>();
        object priorOwner = new();
        tracker.TryBegin(initial.TenantId, priorOwner, sent, out TenantRetainedCommandAttempt? priorAttempt).ShouldBeTrue();
        tracker.TryStartDelivery(priorAttempt!, priorOwner).ShouldBeTrue();
        tracker.TryCompleteDelivery(priorAttempt!, sent, previous).ShouldBeTrue();
        tracker.Detach(priorAttempt, priorOwner);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(x => x.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-edit-metadata-open']").Click();
        IRenderedComponent<EditTenantMetadataFlow> metadata = cut.FindComponent<EditTenantMetadataFlow>();
        metadata.Instance.Snapshot.MessageId.ShouldBe(previous.MessageId);
        metadata.Find("[data-testid='tenants-edit-metadata-name']").Change("Invalidated new intent");
        if (change is "disabled") current = initial with { Status = TenantStatus.Disabled };
        else if (change is "metadata") current = initial with { Name = "Changed after preview" };
        else version = "tenant-sequence:3";
        await metadata.Find("form").SubmitAsync();
        commands.ReceivedCalls().ShouldBeEmpty();
        tracker.HasPendingOwnership(initial.TenantId).ShouldBeFalse();
        metadata.Instance.Snapshot.MessageId.ShouldBe(previous.MessageId);
        metadata.Instance.Snapshot.CorrelationId.ShouldBe(previous.CorrelationId);
        metadata.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Confirmed);
        metadata.Instance.Snapshot.LastConfirmedDetailProjection.ShouldBeSameAs(previous.LastConfirmedDetailProjection);
        ((TenantDetailSnapshot)DetailField(cut, "_snapshot")!).Detail.ShouldNotBeNull().Status.ShouldBe(current.Status);
        ((TenantDetailSnapshot)DetailField(cut, "_snapshot")!).ProjectionVersion.ShouldBe(version);
    }

    /// <summary>A fresh unauthorized read removes protected detail and member data rather than only lowering flags.</summary>
    [Fact]
    public async Task ReviewParentFreshUnauthorizedRemovesProtectedSurfaces()
    {
        TenantDetail initial = Detail("tenant.alpha", new Dictionary<string, string>(), TenantStatus.Active,
            [new TenantMember("operator.alpha", TenantRole.TenantOwner), new TenantMember("private-member-canary", TenantRole.TenantReader)]) with { Name = "private-detail-canary" };
        bool denied = false;
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        RegisterReviewRealRoleParent(commands, _ => Task.FromResult(denied
            ? TenantDetailSnapshot.Unauthorized("tenant.alpha").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha"))
            : ReadyWithSafeConfiguration(initial, ProjectionLifecycleState.Current, "tenant-sequence:1")));
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(x => x.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.FindComponent<AddTenantMemberFlow>().Instance.IsAuthorized.ShouldBeTrue());
        IRenderedComponent<AddTenantMemberFlow> add = cut.FindComponent<AddTenantMemberFlow>();
        add.Find("[data-testid='tenants-add-member-user-id']").Change("next-member");
        FluentSelectInterop.ChangeFluentSelect(add, "tenants-add-member-role", nameof(TenantRole.TenantReader));
        denied = true;
        await add.Find("form").SubmitAsync();
        commands.ReceivedCalls().ShouldBeEmpty();
        cut.WaitForElement("[data-testid='tenants-detail-unauthorized']");
        cut.FindComponents<MemberAccessReview>().ShouldBeEmpty();
        cut.FindComponents<EditTenantMetadataFlow>().ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-member-table']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("private-detail-canary");
        cut.Markup.ShouldNotContain("private-member-canary");
        ((TenantUsersSnapshot)DetailField(cut, "_tenantUsersSnapshot")!).Rows.ShouldBeEmpty();
        Services.GetRequiredService<TenantCommandAttemptTracker>().HasPendingOwnership("tenant.alpha").ShouldBeFalse();
    }

    /// <summary>A same-valued indeterminate fresh result supersedes a genuinely outstanding initial probe.</summary>
    [Fact]
    public async Task ReviewParentFreshIndeterminateSupersedesOlderInitialAuthorizedProbe()
    {
        var pending = new TaskCompletionSource<TenantMutationAuthorizationReflection>(TaskCreationOptions.RunContinuationsAsynchronously);
        int calls = 0;
        CancellationToken oldToken = default;
        var composition = new StubTenantsBffComposition
        {
            MutationResolver = (_, _, token) =>
            {
                if (++calls != 1) return ValueTask.FromResult(TenantMutationAuthorizationReflection.Indeterminate);
                oldToken = token;
                return new(pending.Task);
            },
        };
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:1")), composition);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(x => x.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => calls.ShouldBe(1));
        cut.FindComponent<MemberAccessReview>().Instance.AuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.Indeterminate);
        long oldGeneration = (long)DetailField(cut, "_mutationAuthorizationGeneration")!;
        TenantLifecycleAuthorizationReflectionState answer = await cut.InvokeAsync(() => cut.FindComponent<AddTenantMemberFlow>().Instance.AuthorizationReflectionProvider!(CancellationToken.None));
        answer.ShouldBe(TenantLifecycleAuthorizationReflectionState.Indeterminate);
        oldToken.IsCancellationRequested.ShouldBeTrue();
        ((long)DetailField(cut, "_mutationAuthorizationGeneration")!).ShouldBeGreaterThan(oldGeneration);
        pending.SetResult(new(TenantLifecycleAuthorizationReflectionState.Authorized, TenantLifecycleAuthorizationReflectionState.Authorized));
        await cut.InvokeAsync(async () => await Task.Yield());
        cut.FindComponent<MemberAccessReview>().Instance.AuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.Indeterminate);
        cut.FindComponent<EditTenantMetadataFlow>().Instance.IsAuthorized.ShouldBeFalse();
    }

    /// <summary>Cancelled noncooperative reads cannot replace a newer authorized result on the same page snapshot.</summary>
    [Theory]
    [InlineData(false, "unauthorized")]
    [InlineData(true, "unauthorized")]
    [InlineData(false, "changed")]
    [InlineData(true, "changed")]
    public async Task ReviewParentCancelledFreshReadCannotWithdrawNewerCurrentEvidence(bool timeout, string lateResult)
    {
        TenantDetail initial = Detail("tenant.alpha", new Dictionary<string, string>(), TenantStatus.Active,
            [new TenantMember("operator.alpha", TenantRole.TenantOwner), new TenantMember("reader", TenantRole.TenantReader)]);
        var held = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        int reads = 0;
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        RegisterReviewRealRoleParent(commands, _ => ++reads == 2 ? held.Task
            : Task.FromResult(ReadyWithSafeConfiguration(initial, ProjectionLifecycleState.Current, "tenant-sequence:1")));
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(x => x.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.FindComponent<AddTenantMemberFlow>().Instance.IsAuthorized.ShouldBeTrue());
        TenantDetailSnapshot snapshot = (TenantDetailSnapshot)DetailField(cut, "_snapshot")!;
        using var cancel = timeout ? new CancellationTokenSource(TimeSpan.FromMilliseconds(100)) : new CancellationTokenSource();
        Task<TenantLifecycleAuthorizationReflectionState> stale = cut.InvokeAsync(() => cut.FindComponent<AddTenantMemberFlow>().Instance.AuthorizationReflectionProvider!(cancel.Token));
        cut.WaitForAssertion(() => reads.ShouldBe(2));
        if (!timeout) cancel.Cancel();
        else await Task.Delay(150);
        cancel.IsCancellationRequested.ShouldBeTrue();
        (await cut.InvokeAsync(() => cut.FindComponent<AddTenantMemberFlow>().Instance.AuthorizationReflectionProvider!(CancellationToken.None)))
            .ShouldBe(TenantLifecycleAuthorizationReflectionState.Authorized);
        held.SetResult(lateResult is "unauthorized"
            ? TenantDetailSnapshot.Unauthorized("tenant.alpha").WithCallerScope(snapshot.CallerScope!)
            : ReadyWithSafeConfiguration(initial with { Status = TenantStatus.Disabled, Name = "stale-detail-canary" }, ProjectionLifecycleState.Current, "tenant-sequence:2"));
        await Should.ThrowAsync<OperationCanceledException>(async () => await stale);
        ((TenantDetailSnapshot)DetailField(cut, "_snapshot")!).ShouldBeSameAs(snapshot);
        cut.FindComponent<AddTenantMemberFlow>().Instance.IsAuthorized.ShouldBeTrue();
        cut.FindComponent<EditTenantMetadataFlow>().Instance.IsAuthorized.ShouldBeTrue();
        cut.FindAll("[data-testid='tenants-detail-unauthorized']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("stale-detail-canary");
    }

    /// <summary>Row add reasons reflect current authority independently of unsupported row entry.</summary>
    [Theory]
    [InlineData(TenantLifecycleAuthorizationReflectionState.Authorized, "")]
    [InlineData(TenantLifecycleAuthorizationReflectionState.MissingPermission, "missing permission")]
    [InlineData(TenantLifecycleAuthorizationReflectionState.Indeterminate, "could not be verified")]
    public void ReviewMemberRowAddReasonDistinguishesLiveAuthority(TenantLifecycleAuthorizationReflectionState reflection, string expected)
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(p => p
            .Add(x => x.Detail, detail).Add(x => x.Members, MemberSnapshot(detail))
            .Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current)
            .Add(x => x.Lifecycle, ProjectionLifecycleState.Current).Add(x => x.ProjectionVersion, "tenant-sequence:1")
            .Add(x => x.IsAddMemberAuthorized, reflection is TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(x => x.IsChangeRoleAuthorized, true).Add(x => x.IsRemoveMemberAuthorized, true)
            .Add(x => x.AuthorizationReflection, reflection).Add(x => x.AuditProofCapabilityAvailable, true));
        AngleSharp.Dom.IElement slot = cut.FindAll("[data-testid='tenants-member-action-slot']")
            .First(element => element.GetAttribute("aria-label")!.Contains("Add member", StringComparison.Ordinal));
        string reason = slot.GetAttribute("aria-label")!;
        reason.ShouldContain("high-impact flow not ready");
        if (reflection is TenantLifecycleAuthorizationReflectionState.Authorized)
        {
            reason.ShouldNotContain("missing permission");
            reason.ShouldNotContain("could not be verified");
        }
        else reason.ShouldContain(expected);
    }

    private void RegisterReviewRealRoleParent(ITenantCommandGateway commands, Func<NSubstitute.Core.CallInfo, Task<TenantDetailSnapshot>> detailFactory)
    {
        AuthenticationStateProvider authentication = Substitute.For<AuthenticationStateProvider>();
        authentication.GetAuthenticationStateAsync().Returns(Task.FromResult(new AuthenticationState(
            new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "operator.alpha"), new Claim("global_admin", "false")], "test")))));
        Services.AddSingleton(authentication);
        IUserContextAccessor caller = Substitute.For<IUserContextAccessor>();
        caller.UserId.Returns("operator.alpha");
        var principal = new TenantConfigurationPrincipalResolver(new Hexalith.FrontComposer.Shell.Services.Auth.CircuitServicesAccessor(), caller, authentication);
        RegisterServices(detailFactory, new TenantsBffComposition(commands, principalResolver: principal));
        Services.AddSingleton(commands);
    }

    /// <summary>Parent pages bind the real scoped BFF result to metadata and every member action.</summary>
    [Theory]
    [InlineData(TenantRole.TenantOwner, false, true, true)]
    [InlineData(TenantRole.TenantContributor, false, true, false)]
    [InlineData(TenantRole.TenantReader, false, false, false)]
    [InlineData(TenantRole.TenantReader, true, true, true)]
    [InlineData(TenantRole.Unknown, false, false, false)]
    public void LiveParentBindsScopedRoleAuthority(TenantRole role, bool global, bool metadata, bool membership)
    {
        TenantDetail detail = Detail("tenant.alpha", new Dictionary<string, string>(), TenantStatus.Active,
            role is TenantRole.Unknown ? [new TenantMember("other-owner", TenantRole.TenantOwner)]
                : [new TenantMember("operator.alpha", role), new TenantMember("other-reader", TenantRole.TenantReader)]);
        AuthenticationStateProvider authentication = Substitute.For<AuthenticationStateProvider>();
        authentication.GetAuthenticationStateAsync().Returns(Task.FromResult(new AuthenticationState(global
            ? AdministratorPrincipal()
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "operator.alpha"), new Claim("global_admin", "false")], "test")))));
        Services.AddSingleton(authentication);
        IUserContextAccessor caller = Substitute.For<IUserContextAccessor>();
        caller.UserId.Returns("operator.alpha");
        var principal = new TenantConfigurationPrincipalResolver(
            new Hexalith.FrontComposer.Shell.Services.Auth.CircuitServicesAccessor(), caller, authentication);
        var command = new StubTenantCommandGateway();
        var composition = new TenantsBffComposition(command, principalResolver: principal);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(detail, ProjectionLifecycleState.Current, "tenant-sequence:41")), composition);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(x => x.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() =>
        {
            cut.FindComponent<EditTenantMetadataFlow>().Instance.AuthorizationReflection.ShouldBe(metadata
                ? TenantLifecycleAuthorizationReflectionState.Authorized : TenantLifecycleAuthorizationReflectionState.MissingPermission);
            MemberAccessReview access = cut.FindComponent<MemberAccessReview>().Instance;
            access.IsAddMemberAuthorized.ShouldBe(membership);
            access.IsChangeRoleAuthorized.ShouldBe(membership);
            access.IsRemoveMemberAuthorized.ShouldBe(membership);
            cut.FindComponent<AddTenantMemberFlow>().Instance.IsAuthorized.ShouldBe(membership);
        });
        cut.FindAll("[data-testid='tenants-edit-metadata-open']").Count.ShouldBe(metadata ? 1 : 0);
        if (!membership)
        {
            cut.FindAll("[data-testid='tenants-change-role-open']").ShouldBeEmpty();
            cut.FindAll("[data-testid='tenants-remove-member-open']").ShouldBeEmpty();
        }
    }

    /// <summary>Contributor metadata and Owner/GA membership submissions succeed through the real scoped BFF.</summary>
    [Theory]
    [InlineData(TenantRole.TenantContributor, false, "metadata")]
    [InlineData(TenantRole.TenantOwner, false, "add")]
    [InlineData(TenantRole.TenantReader, true, "add")]
    [InlineData(TenantRole.Unknown, true, "first-member")]
    public async Task LiveParentAuthorizedSubmissionsUseRealScopedRoleReflection(TenantRole role, bool global, string kind)
    {
        TenantDetail detail = Detail("tenant.alpha", new Dictionary<string, string>(), TenantStatus.Active,
            kind is "first-member" ? [] : [new TenantMember("operator.alpha", role), new TenantMember("other-reader", TenantRole.TenantReader)]);
        AuthenticationStateProvider authentication = Substitute.For<AuthenticationStateProvider>();
        authentication.GetAuthenticationStateAsync().Returns(Task.FromResult(new AuthenticationState(global
            ? AdministratorPrincipal()
            : new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "operator.alpha"), new Claim("global_admin", "false")], "test")))));
        Services.AddSingleton(authentication);
        IUserContextAccessor caller = Substitute.For<IUserContextAccessor>();
        caller.UserId.Returns("operator.alpha");
        var principal = new TenantConfigurationPrincipalResolver(new Hexalith.FrontComposer.Shell.Services.Auth.CircuitServicesAccessor(), caller, authentication);
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        commands.UpdateTenantAsync(Arg.Any<UpdateTenant>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(call.ArgAt<string>(1), "metadata-tracking")));
        commands.AddUserToTenantAsync(Arg.Any<AddUserToTenant>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(TenantCommandSubmissionResult.Accepted(call.ArgAt<string>(1), "member-tracking")));
        commands.GetStatusAsync(Arg.Any<TenantCommandTrackingHandle>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(new TenantCommandStatusResult(CommandStatus.Received, HasVerifiedCommandIdentity: true)));
        var composition = new TenantsBffComposition(commands, principalResolver: principal);
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(detail, ProjectionLifecycleState.Current, "tenant-sequence:1")), composition);
        Services.AddSingleton(commands);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(x => x.TenantId, "tenant.alpha"));
        if (kind is "metadata")
        {
            cut.WaitForElement("[data-testid='tenants-edit-metadata-open']").Click();
            IRenderedComponent<EditTenantMetadataFlow> flow = cut.FindComponent<EditTenantMetadataFlow>();
            flow.Find("[data-testid='tenants-edit-metadata-name']").Change("Contributor updated tenant");
            await flow.Find("form").SubmitAsync();
            _ = commands.Received(1).UpdateTenantAsync(Arg.Is<UpdateTenant>(request => request.TenantId == "tenant.alpha" && request.Name == "Contributor updated tenant"), Arg.Any<string>(), Arg.Any<CancellationToken>());
            flow.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted);
            flow.Instance.Snapshot.MessageId.ShouldNotBeNullOrWhiteSpace();
        }
        else
        {
            IRenderedComponent<AddTenantMemberFlow> flow = cut.FindComponent<AddTenantMemberFlow>();
            cut.WaitForAssertion(() => flow.Instance.IsAuthorized.ShouldBeTrue());
            flow.Find("[data-testid='tenants-add-member-user-id']").Change("new-literal-member");
            FluentSelectInterop.ChangeFluentSelect(flow, "tenants-add-member-role", kind is "first-member" ? nameof(TenantRole.TenantOwner) : nameof(TenantRole.TenantReader));
            await flow.Find("form").SubmitAsync();
            flow.Instance.Snapshot.State.ShouldBe(TenantCommandLifecycleState.Accepted, flow.Instance.Snapshot.SafeMessage);
            _ = commands.Received(1).AddUserToTenantAsync(Arg.Is<AddUserToTenant>(request => request.TenantId == "tenant.alpha" && request.UserId == "new-literal-member"), Arg.Any<string>(), Arg.Any<CancellationToken>());
            flow.Instance.Snapshot.MessageId.ShouldNotBeNullOrWhiteSpace();
        }
    }

    /// <summary>Fresh revocation and unauthorized authority reads disable sibling launchers without dispatch.</summary>
    [Theory]
    [InlineData("role-revoked")]
    [InlineData("unauthorized")]
    [InlineData("indeterminate")]
    public async Task LiveParentPropagatesFreshAdmissionRefusalToAllMutationBindings(string refusal)
    {
        TenantDetail detail = Detail("tenant.alpha");
        bool fresh = false;
        var composition = new StubTenantsBffComposition();
        RegisterServices(_ => Task.FromResult(fresh && refusal is "unauthorized"
            ? TenantDetailSnapshot.Unauthorized("tenant.alpha").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha"))
            : ReadyWithSafeConfiguration(detail, ProjectionLifecycleState.Current, "tenant-sequence:41")), composition);
        ITenantCommandGateway commands = Substitute.For<ITenantCommandGateway>();
        Services.AddSingleton(commands);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(x => x.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.FindComponent<MemberAccessReview>().Instance.IsAddMemberAuthorized.ShouldBeTrue());
        IRenderedComponent<AddTenantMemberFlow> add = cut.FindComponent<AddTenantMemberFlow>();
        add.Find("[data-testid='tenants-add-member-user-id']").Change("literal-next-user");
        FluentSelectInterop.ChangeFluentSelect(add, "tenants-add-member-role", nameof(TenantRole.TenantReader));
        TenantLifecycleAuthorizationReflectionState denied = refusal is "indeterminate"
            ? TenantLifecycleAuthorizationReflectionState.Indeterminate : TenantLifecycleAuthorizationReflectionState.MissingPermission;
        composition.MutationReflection = new(denied, denied);
        fresh = true;
        await add.Find("form").SubmitAsync();
        commands.ReceivedCalls().ShouldBeEmpty();
        if (refusal is "unauthorized")
        {
            cut.WaitForElement("[data-testid='tenants-detail-unauthorized']");
            cut.FindComponents<MemberAccessReview>().ShouldBeEmpty();
            cut.FindComponents<EditTenantMetadataFlow>().ShouldBeEmpty();
            return;
        }
        cut.WaitForAssertion(() =>
        {
            MemberAccessReview access = cut.FindComponent<MemberAccessReview>().Instance;
            access.AuthorizationReflection.ShouldBe(denied);
            access.IsAddMemberAuthorized.ShouldBeFalse();
            access.IsChangeRoleAuthorized.ShouldBeFalse();
            access.IsRemoveMemberAuthorized.ShouldBeFalse();
            cut.FindComponent<EditTenantMetadataFlow>().Instance.AuthorizationReflection.ShouldBe(denied);
        });
        cut.FindAll("[data-testid='tenants-edit-metadata-open']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-change-role-open']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-remove-member-open']").ShouldBeEmpty();
    }

    /// <summary>A delayed scoped lookup cannot authorize newer tenant, caller, or projection evidence.</summary>
    [Theory]
    [InlineData("tenant")]
    [InlineData("caller")]
    [InlineData("detail")]
    public async Task LiveParentRejectsSupersededMutationReflection(string transition)
    {
        var pending = new TaskCompletionSource<TenantMutationAuthorizationReflection>(TaskCreationOptions.RunContinuationsAsynchronously);
        int lookups = 0;
        var composition = new StubTenantsBffComposition
        {
            MutationResolver = (_, _, _) => ++lookups == 1 ? new(pending.Task)
                : ValueTask.FromResult(new TenantMutationAuthorizationReflection(TenantLifecycleAuthorizationReflectionState.MissingPermission, TenantLifecycleAuthorizationReflectionState.MissingPermission)),
        };
        var authentication = new MutableAuthenticationStateProvider();
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        RegisterServices(call => Task.FromResult(ReadyWithSafeConfiguration(Detail(call.Arg<TenantDetailRequest>().TenantId), ProjectionLifecycleState.Current, $"tenant-sequence:{lookups + 41}")), composition);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(x => x.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => lookups.ShouldBe(1));
        cut.FindAll("[data-testid='tenants-edit-metadata-open']").ShouldBeEmpty();
        if (transition is "tenant") cut.Render(p => p.Add(x => x.TenantId, "tenant.beta"));
        else if (transition is "caller") await cut.InvokeAsync(() => authentication.Notify(new ClaimsPrincipal(new ClaimsIdentity([new Claim("sub", "operator.next")], "test"))));
        else await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance.OnProjectionRefreshRequested.InvokeAsync());
        pending.SetResult(new(TenantLifecycleAuthorizationReflectionState.Authorized, TenantLifecycleAuthorizationReflectionState.Authorized));
        await cut.InvokeAsync(() => Task.CompletedTask);
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-edit-metadata-open']").ShouldBeEmpty());
        cut.FindAll("[data-testid='tenants-change-role-open']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-remove-member-open']").ShouldBeEmpty();
    }

    /// <summary>Older render authority must not overwrite a fresh admission refusal on the same snapshot.</summary>
    [Fact]
    public async Task LiveParentFreshDenialSupersedesDelayedInitialReflection()
    {
        var pending = new TaskCompletionSource<TenantMutationAuthorizationReflection>(TaskCreationOptions.RunContinuationsAsynchronously);
        int lookups = 0;
        var composition = new StubTenantsBffComposition();
        RegisterServices(_ => Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"), ProjectionLifecycleState.Current, "tenant-sequence:41")), composition);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(x => x.TenantId, "tenant.alpha"));
        cut.WaitForAssertion(() => cut.FindComponent<MemberAccessReview>().Instance.IsAddMemberAuthorized.ShouldBeTrue());
        composition.MutationResolver = (_, _, _) => ++lookups == 1 ? new(pending.Task)
            : ValueTask.FromResult(new TenantMutationAuthorizationReflection(TenantLifecycleAuthorizationReflectionState.MissingPermission, TenantLifecycleAuthorizationReflectionState.MissingPermission));
        TenantDetailSnapshot snapshot = (TenantDetailSnapshot)DetailField(cut, "_snapshot")!;
        long generation = (long)DetailField(cut, "_mutationAuthorizationGeneration")!;
        var source = new CancellationTokenSource();
        Task olderRender = cut.InvokeAsync(() => (Task)typeof(TenantDetailPage)
            .GetMethod("ResolveMutationAuthorizationAsync", BindingFlags.Instance | BindingFlags.NonPublic)!
            .Invoke(cut.Instance, [snapshot, generation, source])!);
        cut.WaitForAssertion(() => lookups.ShouldBe(1));
        AddTenantMemberFlow add = cut.FindComponent<AddTenantMemberFlow>().Instance;
        TenantLifecycleAuthorizationReflectionState fresh = await cut.InvokeAsync(() => add.AuthorizationReflectionProvider!(CancellationToken.None));
        fresh.ShouldBe(TenantLifecycleAuthorizationReflectionState.MissingPermission);
        cut.WaitForAssertion(() => cut.FindComponent<MemberAccessReview>().Instance.AuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.MissingPermission));
        pending.SetResult(new(TenantLifecycleAuthorizationReflectionState.Authorized, TenantLifecycleAuthorizationReflectionState.Authorized));
        await olderRender.WaitAsync(TimeSpan.FromSeconds(5));
        cut.FindComponent<MemberAccessReview>().Instance.AuthorizationReflection.ShouldBe(TenantLifecycleAuthorizationReflectionState.MissingPermission);
        cut.FindComponent<EditTenantMetadataFlow>().Instance.IsAuthorized.ShouldBeFalse();
        cut.FindAll("[data-testid='tenants-edit-metadata-open']").ShouldBeEmpty();
    }

    /// <summary>Direct row launchers cannot bypass denied or indeterminate member flags.</summary>
    [Theory]
    [InlineData(TenantLifecycleAuthorizationReflectionState.MissingPermission)]
    [InlineData(TenantLifecycleAuthorizationReflectionState.Indeterminate)]
    public async Task LiveMemberReviewDirectLaunchersFailClosed(TenantLifecycleAuthorizationReflectionState authority)
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(p => p
            .Add(x => x.Detail, detail).Add(x => x.Members, MemberSnapshot(detail))
            .Add(x => x.SurfaceKind, TenantDetailSurfaceKind.Ready).Add(x => x.Freshness, ReadModelFreshnessState.Current)
            .Add(x => x.AuthorizationReflection, authority)
            .Add(x => x.IsAddMemberAuthorized, false).Add(x => x.IsChangeRoleAuthorized, false).Add(x => x.IsRemoveMemberAuthorized, false)
            .Add(x => x.AuditProofCapabilityAvailable, true));
        await cut.InvokeAsync(() =>
        {
            typeof(MemberAccessReview).GetMethod("OpenChangeRole", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cut.Instance, [detail.Members[1]]);
            typeof(MemberAccessReview).GetMethod("OpenRemoveMember", BindingFlags.Instance | BindingFlags.NonPublic)!.Invoke(cut.Instance, [detail.Members[1]]);
        });
        cut.Render();
        cut.FindComponents<ChangeTenantMemberRoleFlow>().ShouldBeEmpty();
        cut.FindComponents<RemoveTenantMemberFlow>().ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-change-role-open']").ShouldBeEmpty();
        cut.FindAll("[data-testid='tenants-remove-member-open']").ShouldBeEmpty();
    }

    private void RegisterServices(TenantListSnapshot snapshot)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(snapshot));
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        // The workspace only restores retained protected paging on an interactive render pass.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private void RegisterListServices(Func<NSubstitute.Core.CallInfo, Task<TenantListSnapshot>> resultFactory)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.ListTenantsAsync(Arg.Any<TenantListRequest>(), Arg.Any<TenantListSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(resultFactory);
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        // The workspace only restores retained protected paging on an interactive render pass.
        SetRendererInfo(new RendererInfo("Server", isInteractive: true));
    }

    private void RegisterMembershipPageServices(
        ITenantCommandGateway commandGateway,
        ITenantsBffComposition bffComposition)
    {
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ReadyWithSafeConfiguration(
                Detail(call.Arg<TenantDetailRequest>().TenantId),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>().TenantId)) with
            {
                ProjectionVersion = "projection-v1",
            }));
        ConfigureAuthoritativeAuditCapability(gateway);
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton(commandGateway);
        Services.AddSingleton(bffComposition);
        Services.AddFluentUIComponents();
    }

    private void RegisterServices(
        Func<NSubstitute.Core.CallInfo, Task<TenantDetailSnapshot>> detailFactory,
        ITenantsBffComposition? bffComposition = null)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        TenantDetail? observedDetail = null;
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(async call =>
            {
                TenantDetailSnapshot snapshot = await detailFactory(call);
                observedDetail = snapshot.Detail;
                return snapshot;
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantUsersRequest request = call.Arg<TenantUsersRequest>()
                    ?? throw new InvalidOperationException("A tenant-users request is required.");
                return Task.FromResult(MemberSnapshot(observedDetail ?? Detail(request.TenantId)));
            });
        ConfigureAuthoritativeAuditCapability(gateway);
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton(bffComposition ?? new StubTenantsBffComposition());
        Services.AddFluentUIComponents();
    }

    private static void ConfigureAuthoritativeAuditCapability(ITenantQueryGateway gateway)
        => gateway.GetTenantAuditAsync(
                Arg.Any<TenantAuditRequest>(),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantAuditRequest request = call.ArgAt<TenantAuditRequest>(0);
                return TenantAuditSnapshot.Empty(
                    isAuthorizationScoped: true,
                    ReadModelFreshnessState.Current,
                    eTag: "audit-capability-etag",
                    request) with
                {
                    CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                    Lifecycle = ProjectionLifecycleState.Current,
                    ProjectionVersion = "audit-capability-v1",
                };
            });

    private static void ConfigureMetadataAuditProof(
        ITenantQueryGateway gateway,
        Func<TenantAuditRequest, TenantAuditSnapshot> proofFactory)
        => gateway.GetTenantAuditAsync(
                Arg.Is<TenantAuditRequest>(request =>
                    request.From.HasValue
                    && request.Category == AuditEventCategory.Administrative),
                Arg.Any<TenantAuditSnapshot?>(),
                Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(proofFactory(call.ArgAt<TenantAuditRequest>(0))));

    private static TenantAuditSnapshot MetadataAuditSnapshot(
        TenantAuditRequest request,
        TenantAuditRow row,
        bool hasMore = false,
        string projectionVersion = "audit-proof-v2")
        => MetadataAuditSnapshot(request, [row], hasMore, projectionVersion);

    private static TenantAuditSnapshot MetadataAuditSnapshot(
        TenantAuditRequest request,
        IReadOnlyList<TenantAuditRow> rows,
        bool hasMore = false,
        string projectionVersion = "audit-proof-v2")
        => TenantAuditSnapshot.Ready(
            rows,
            hasMore ? "opaque-next-page" : null,
            hasMore,
            "audit-proof-etag",
            ReadModelFreshnessState.Current,
            request) with
        {
            CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
            Lifecycle = ProjectionLifecycleState.Current,
            ProjectionVersion = projectionVersion,
        };

    private static TenantAuditRow MetadataAuditRow(
        string messageId,
        string tenantId,
        DateTimeOffset timestamp)
        => new(
            messageId,
            "TenantUpdated",
            AuditEventCategory.Administrative,
            "operator-user",
            timestamp,
            tenantId,
            tenantId,
            tenantId,
            "TenantUpdated",
            string.Empty,
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current,
            QueryResponseProvenance.ProjectionBacked);

    private static TenantDetailSnapshot Snapshot(TenantDetailSurfaceKind kind)
        => kind switch
        {
            TenantDetailSurfaceKind.Stale => TenantDetailSnapshot.Stale(Detail("tenant.alpha"), "\"etag\"").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha")),
            TenantDetailSurfaceKind.Degraded => TenantDetailSnapshot.Degraded(Detail("tenant.alpha"), "Tenant detail projection is degraded.").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha")),
            TenantDetailSurfaceKind.Unauthorized => TenantDetailSnapshot.Unauthorized("tenant.alpha"),
            TenantDetailSurfaceKind.NotFound => TenantDetailSnapshot.NotFound("tenant.alpha"),
            TenantDetailSurfaceKind.Unavailable => TenantDetailSnapshot.Unavailable("Tenant detail query gateway is unavailable."),
            TenantDetailSurfaceKind.Unknown => TenantDetailSnapshot.Unknown("Tenant detail projection returned no payload.").WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha")),
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null),
        };

    private static string ProjectRoot()
        => Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

    /// <summary>
    /// Returns the element-reference id passed to each focus interop call, in call order.
    /// </summary>
    /// <remarks>
    /// <c>ElementReference.FocusAsync</c> routes every element through one interop identifier, so counting
    /// calls proves only that <em>something</em> was focused. The id identifies which element it was.
    /// </remarks>
    private IReadOnlyList<string> FocusedElementIds()
        => [.. JSInterop.Invocations
            .Where(invocation => invocation.Identifier.Contains("focus", StringComparison.OrdinalIgnoreCase))
            .Select(invocation => invocation.Arguments.Count > 0 && invocation.Arguments[0] is ElementReference reference
                ? reference.Id
                : string.Empty)];

    /// <summary>
    /// Reads an <see cref="ElementReference"/> a component captured with <c>@ref</c>.
    /// </summary>
    /// <remarks>
    /// Read-only, and deliberately narrow. bUnit renders the <c>blazor:elementreference</c> marker without
    /// its id, so a focus target cannot be correlated to a DOM element through markup and there is no public
    /// surface exposing it. The alternative -- asserting only that some focus call happened -- cannot
    /// distinguish the two focus paths this component has, which is the whole point of the assertion.
    /// </remarks>
    private static string CapturedElementReferenceId(object component, string fieldName)
    {
        object value = component.GetType()
            .GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(component)
            ?? throw new InvalidOperationException(
                $"'{fieldName}' is not a field of {component.GetType().Name}; the focus assertion cannot "
                + "identify its target and would otherwise silently weaken to a call count.");
        return ((ElementReference)value).Id;
    }

    /// <summary>Reads the captured launch-control reference for one configuration row.</summary>
    private static string CapturedLaunchElementReferenceId(object component, string key)
    {
        object value = component.GetType()
            .GetField("_removeLaunchElements", BindingFlags.NonPublic | BindingFlags.Instance)
            ?.GetValue(component)
            ?? throw new InvalidOperationException("'_removeLaunchElements' is not a field of the component.");
        Dictionary<string, ElementReference> elements = (Dictionary<string, ElementReference>)value;
        elements.TryGetValue(key, out ElementReference reference).ShouldBeTrue(
            $"No launch control was captured for '{key}', so focus cannot have returned to it.");
        return reference.Id;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, TimeSpan timeout)
    {
        DateTimeOffset deadline = DateTimeOffset.UtcNow + timeout;
        while (!condition())
        {
            if (DateTimeOffset.UtcNow > deadline)
            {
                throw new TimeoutException("The awaited condition was not met within the timeout.");
            }

            await Task.Delay(10);
        }
    }

    private static TenantDetail Detail(string tenantId)
        => Detail(tenantId, new Dictionary<string, string>
        {
            ["billing.mode"] = "trial",
        });

    [Fact]
    public void Configuration_view_never_renders_error_metadata_correlation_ids_tokens_stack_traces_or_pii()
    {
        // Restores the support-safety regression guard deleted by the read/management split. Its ten
        // assertions were the only executable evidence for the story's "never expose correlations,
        // exceptions, stack traces, tokens or PII" clause on this surface, and they were removed in the
        // same change that added new exception-handling paths through the gateway and composer.
        RegisterComponentServices();

        IRenderedComponent<TenantConfigurationView> cut = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, TenantConfigurationSafeModel.Unavailable("tenant.alpha"))
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Unavailable)
            .Add(view => view.Freshness, ReadModelFreshnessState.Unknown));

        foreach (string forbidden in new[]
        {
            "correlation-123",
            "raw-cursor",
            "InvalidOperationException",
            "stack trace",
            "at Hexalith.",
            "eyJhbGciOiJIUzI1NiJ9",
            "Bearer ",
            "jane.doe@example.test",
            "EventStore metadata",
            "ProjectedAt",
        })
        {
            cut.Markup.ShouldNotContain(forbidden, Case.Insensitive);
        }
    }

    [Fact]
    public void Configuration_filter_does_not_normalize_or_ignore_literal_code_points()
    {
        // Ordinal comparison keeps canonical composition and zero-width characters literal. Code points
        // are spelled out so the intent cannot be lost to editor normalization.
        const string composed = "billing.caf\u00E9";      // NFC: e-acute as one code point
        const string decomposed = "caf\u0065\u0301";      // NFD: e + combining acute
        const string zeroWidthSpace = "\u200B";

        RegisterComponentServices();
        TenantConfigurationSafeModel model = SafeConfiguration(("billing", composed, "trial"));

        IRenderedComponent<TenantConfigurationView> cut = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, model)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current));

        cut.Find("[data-testid='tenants-config-read-filter']").Change(decomposed);
        cut.FindAll("[data-testid='tenants-config-read-group']").ShouldBeEmpty();

        cut.Find("[data-testid='tenants-config-read-filter']").Change(zeroWidthSpace);
        cut.FindAll("[data-testid='tenants-config-read-group']").ShouldBeEmpty();

        cut.Find("[data-testid='tenants-config-read-filter']").Change("caf\u00E9");
        cut.FindAll("[data-testid='tenants-config-read-group']").Count.ShouldBe(1);
    }

    [Fact]
    public void Configuration_filter_is_ordinal_and_case_sensitive()
    {
        RegisterComponentServices();
        TenantConfigurationSafeModel model = SafeConfiguration(
            ("billing", "billing.mode", "lower"),
            ("billing", "billing.Mode", "upper"));

        IRenderedComponent<TenantConfigurationView> cut = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, model)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current));

        cut.Find("[data-testid='tenants-config-read-filter']").Change("Mode");

        IElement key = cut.Find("[data-testid='tenants-config-read-key']");
        key.TextContent.ShouldBe("billing.Mode");
        cut.Markup.ShouldNotContain(">billing.mode<", Case.Sensitive);
    }

    [Fact]
    public void Configuration_filter_treats_whitespace_only_input_as_literal_data()
    {
        RegisterComponentServices();
        TenantConfigurationSafeModel model = SafeConfiguration(
            ("billing", "billing.mode", "compact"),
            ("billing", "billing mode", "spaced"));

        IRenderedComponent<TenantConfigurationView> cut = Render<TenantConfigurationView>(parameters => parameters
            .Add(view => view.Model, model)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current));

        cut.Find("[data-testid='tenants-config-read-filter']").Change(" ");

        IElement key = cut.Find("[data-testid='tenants-config-read-key']");
        key.TextContent.ShouldBe("billing mode");
        cut.Markup.ShouldNotContain(">billing.mode<", Case.Sensitive);
    }

    private static TenantConfigurationSafeModel SafeConfiguration(
        params (string Namespace, string Key, string Value)[] rows)
        => TenantConfigurationSafeModel.Available(
            "tenant.alpha",
            rows.Select(static row => new TenantConfigurationSafeRow(row.Namespace, row.Key, row.Value)));

    private static TenantHighImpactActionEvidence ManagementEvidence(
        TenantConfigurationManagementContext context,
        TenantHighImpactAction action)
        => new(
            context.TenantId,
            action,
            TenantHighImpactEvaluationStage.PreviewEntry,
            context.TenantStatus,
            TenantHighImpactFreshnessState.Current,
            HasCurrentBaseline: true,
            TenantDetailSurfaceKind.Ready,
            ProjectionLifecycleState.Current,
            context.HasMutationAuthority
                ? TenantHighImpactAuthorityEvidence.Authorized
                : TenantHighImpactAuthorityEvidence.MissingPermission,
            context.IsGlobalAdministrator || context.AuthorizedPrefixes.Count > 0
                ? TenantHighImpactNamespaceScopeEvidence.Authorized
                : TenantHighImpactNamespaceScopeEvidence.Missing,
            TenantHighImpactSupportEvidence.Ready,
            TenantHighImpactAdmissionEvidence.Available,
            TenantHighImpactPreviewEvidence.Ready,
            TenantHighImpactProofEvidence.NotRequired,
            TenantHighImpactViewportState.Safe,
            IsInputComplete: false,
            TenantHighImpactTargetState.Unknown,
            ProjectionVersion: null);

    private static TenantDetailSnapshot ReadyWithSafeConfiguration(
        TenantDetail detail,
        ProjectionLifecycleState lifecycle = ProjectionLifecycleState.Current,
        string? projectionVersion = null)
    {
        TenantConfigurationSafeRow[] rows = detail.Configuration
            .Select(static item => new TenantConfigurationSafeRow(NamespaceFrom(item.Key), item.Key, item.Value))
            .ToArray();
        TenantConfigurationComposition composition = new(
            TenantConfigurationSafeComposer.SanitizeDetail(detail),
            TenantConfigurationSafeModel.Available(detail.TenantId, rows),
            TenantConfigurationManagementContext.Available(
                detail.TenantId,
                detail.Status,
                isGlobalAdministrator: false,
                rows.Select(static row => row.Namespace).Distinct(StringComparer.Ordinal),
                rows,
                TenantConfigurationAuthorityState.TenantOwner));
        return TenantDetailSnapshot.Ready(
            composition,
            "\"etag\"",
            ReadModelFreshnessState.Current,
            lifecycle,
            projectionVersion).WithCallerScope(TenantQueryGateway.AuditCallerScope("operator.alpha"));
    }

    private static TenantUsersSnapshot MemberSnapshot(TenantDetail detail)
        => detail.Members.Count == 0
            ? TenantUsersSnapshot.Empty(
                detail.TenantId,
                isAuthorizationScoped: true,
                eTag: "members-etag",
                projectionVersion: "tenant-sequence:1",
                ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") }
            : TenantUsersSnapshot.Ready(
                detail.TenantId,
                detail.Members,
                nextCursor: null,
                hasMore: false,
                eTag: "members-etag",
                projectionVersion: "tenant-sequence:1",
                ReadModelFreshnessState.Current,
                ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };

    private static string NamespaceFrom(string key)
    {
        int separator = key.IndexOf('.', StringComparison.Ordinal);
        return separator > 0 ? key[..separator] : key;
    }

    private static TenantDetail Detail(string tenantId, IReadOnlyDictionary<string, string> configuration)
        => Detail(
            tenantId,
            configuration,
            TenantStatus.Active,
            [
                new TenantMember("owner-user", TenantRole.TenantOwner),
                new TenantMember("reader-user", TenantRole.TenantReader),
            ]);

    private static TenantDetail Detail(
        string tenantId,
        IReadOnlyDictionary<string, string> configuration,
        TenantStatus status,
        IReadOnlyList<TenantMember> members)
        => new(
            tenantId,
            "Alpha",
            "Tenant alpha description",
            status,
            members,
            configuration,
            DateTimeOffset.Parse("2026-06-01T12:00:00Z", CultureInfo.InvariantCulture));

    private sealed class TrackingMembershipCommandGateway(
        Task<TenantCommandSubmissionResult> addMemberSubmission,
        TenantCommandStatusResult? status = null) : ITenantCommandGateway
    {
        public int AddMemberCallCount { get; private set; }

        public int ChangeRoleCallCount { get; private set; }

        public int RemoveMemberCallCount { get; private set; }

        public int StatusCallCount { get; private set; }

        public Task<TenantCommandSubmissionResult> CreateTenantAsync(CreateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> AddUserToTenantAsync(AddUserToTenant request, string? messageId = null, CancellationToken cancellationToken = default)
        {
            AddMemberCallCount++;
            return addMemberSubmission;
        }

        public Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(ChangeUserRole request, string? messageId = null, CancellationToken cancellationToken = default)
        {
            ChangeRoleCallCount++;
            return addMemberSubmission;
        }

        public Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(RemoveUserFromTenant request, string? messageId = null, CancellationToken cancellationToken = default)
        {
            RemoveMemberCallCount++;
            return addMemberSubmission;
        }

        public Task<TenantCommandSubmissionResult> UpdateTenantAsync(UpdateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(SetTenantConfiguration request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> RemoveTenantConfigurationAsync(RemoveTenantConfiguration request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandStatusResult> GetStatusAsync(TenantCommandTrackingHandle handle, CancellationToken cancellationToken = default)
        {
            StatusCallCount++;
            return Task.FromResult(status ?? TenantCommandStatusResult.Unknown("Command status is unavailable."));
        }
    }

    private sealed class TrackingLifecycleCommandGateway : ITenantCommandGateway
    {
        public bool SupportsTrackedLifecycleDispatch => true;

        public TenantCommandStatusResult Status { get; set; } = new(
            CommandStatus.Completed,
            EventCount: 1,
            HasVerifiedCommandIdentity: true);

        public int DisableCallCount { get; private set; }

        public int StatusCallCount { get; private set; }

        public TenantLifecycleCommandRequest? LastDisableRequest { get; private set; }

        public string? LastMessageId { get; private set; }

        public Task<TenantCommandSubmissionResult> CreateTenantAsync(CreateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> AddUserToTenantAsync(AddUserToTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(ChangeUserRole request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(RemoveUserFromTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> UpdateTenantAsync(UpdateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(SetTenantConfiguration request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> DisableTenantTrackedAsync(
            TenantLifecycleCommandRequest request,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            DisableCallCount++;
            LastDisableRequest = request;
            LastMessageId = messageId;
            return Task.FromResult(TenantCommandSubmissionResult.Accepted(
                messageId ?? string.Empty,
                "correlation-life"));
        }

        public Task<TenantCommandStatusResult> GetStatusAsync(
            TenantCommandTrackingHandle handle,
            CancellationToken cancellationToken = default)
        {
            StatusCallCount++;
            return Task.FromResult(Status);
        }
    }

    private sealed class TrackingRemoveConfigurationCommandGateway : ITenantCommandGateway
    {
        public bool SupportsTrackedRemoveConfigurationDispatch => true;

        public TenantCommandStatusResult Status { get; set; }
            = TenantCommandStatusResult.Pending("Command status is pending.");

        public int RemoveCallCount { get; private set; }

        public int StatusCallCount { get; private set; }

        public TenantCommandTrackingHandle? LastStatusHandle { get; private set; }

        public Task<TenantCommandSubmissionResult> CreateTenantAsync(
            CreateTenant request,
            string? messageId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> AddUserToTenantAsync(
            AddUserToTenant request,
            string? messageId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(
            ChangeUserRole request,
            string? messageId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(
            RemoveUserFromTenant request,
            string? messageId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> UpdateTenantAsync(
            UpdateTenant request,
            string? messageId = null,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(
            SetTenantConfiguration request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> RemoveTenantConfigurationAsync(
            RemoveTenantConfiguration request,
            CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> RemoveTenantConfigurationTrackedAsync(
            RemoveTenantConfiguration request,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            RemoveCallCount++;
            return Task.FromResult(TenantCommandSubmissionResult.Accepted(messageId, messageId));
        }

        public Task<TenantCommandStatusResult> GetStatusAsync(
            TenantCommandTrackingHandle handle,
            CancellationToken cancellationToken = default)
        {
            StatusCallCount++;
            LastStatusHandle = handle;
            return Task.FromResult(Status);
        }
    }

    private sealed class StubTenantCommandGateway : ITenantCommandGateway
    {
        public bool SupportsTrackedSetConfigurationDispatch => true;

        public TenantCommandSubmissionResult AddMemberSubmission { get; init; }
            = TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.");

        public TenantCommandSubmissionResult SetConfigurationSubmission { get; init; }
            = TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable.");

        public TenantCommandStatusResult Status { get; init; }
            = TenantCommandStatusResult.Unknown("Tenant command status is unavailable.");

        public int SetConfigurationCallCount { get; private set; }

        public int StatusCallCount { get; private set; }

        public SetTenantConfiguration? LastSetConfigurationRequest { get; private set; }

        public string? LastMessageId { get; private set; }

        public TenantCommandTrackingHandle? LastStatusHandle { get; private set; }

        public Task<TenantCommandSubmissionResult> CreateTenantAsync(CreateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Not used."));

        public Task<TenantCommandSubmissionResult> AddUserToTenantAsync(AddUserToTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(AddMemberSubmission);

        public Task<TenantCommandSubmissionResult> ChangeUserRoleAsync(ChangeUserRole request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable."));

        public Task<TenantCommandSubmissionResult> RemoveUserFromTenantAsync(RemoveUserFromTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable."));

        public Task<TenantCommandSubmissionResult> UpdateTenantAsync(UpdateTenant request, string? messageId = null, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable."));

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationAsync(SetTenantConfiguration request, CancellationToken cancellationToken = default)
        {
            SetConfigurationCallCount++;
            LastSetConfigurationRequest = request;
            return Task.FromResult(SetConfigurationSubmission);
        }

        public Task<TenantCommandSubmissionResult> SetTenantConfigurationTrackedAsync(
            SetTenantConfiguration request,
            string messageId,
            CancellationToken cancellationToken = default)
        {
            SetConfigurationCallCount++;
            LastSetConfigurationRequest = request;
            LastMessageId = messageId;
            return Task.FromResult(SetConfigurationSubmission with { MessageId = messageId });
        }

        public Task<TenantCommandSubmissionResult> RemoveTenantConfigurationAsync(RemoveTenantConfiguration request, CancellationToken cancellationToken = default)
            => Task.FromResult(TenantCommandSubmissionResult.Failed("Tenant command gateway is unavailable."));

        public Task<TenantCommandStatusResult> GetStatusAsync(TenantCommandTrackingHandle handle, CancellationToken cancellationToken = default)
        {
            StatusCallCount++;
            LastStatusHandle = handle;
            return Task.FromResult(Status);
        }
    }

    private sealed class StubTenantsBffComposition(
        TenantLifecycleAuthorizationReflectionState lifecycleReflection
            = TenantLifecycleAuthorizationReflectionState.Indeterminate,
        bool commandSurfaceConnected = true,
        bool readSurfaceConnected = true) : ITenantsBffComposition
    {
        private TenantLifecycleAuthorizationReflectionState _reflection = lifecycleReflection;

        public bool IsReadSurfaceConnected => readSurfaceConnected;

        public bool IsCommandSurfaceConnected => commandSurfaceConnected;

        /// <summary>Arms a one-shot suspension of the next lifecycle authorization resolve.</summary>
        public TaskCompletionSource? ResolutionGate { get; set; }

        /// <summary>Completes when a gated lifecycle authorization resolve has entered and suspended.</summary>
        public TaskCompletionSource ResolutionEntered { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>Counts async lifecycle authorization resolutions.</summary>
        public int AsyncResolutionCount { get; private set; }

        public TenantLifecycleAuthorizationReflectionState Reflection
        {
            get => _reflection;
            set => _reflection = value;
        }

        public async ValueTask<TenantLifecycleAuthorizationReflectionState> ResolveLifecycleAuthorizationAsync(
            CancellationToken cancellationToken = default)
        {
            AsyncResolutionCount++;
            TenantLifecycleAuthorizationReflectionState answer = Reflection;
            TaskCompletionSource? gate = ResolutionGate;
            if (gate is not null)
            {
                ResolutionGate = null;
                _ = ResolutionEntered.TrySetResult();
                await gate.Task.ConfigureAwait(false);
            }

            return answer;
        }

        public TenantMutationAuthorizationReflection MutationReflection { get; set; } = new(
            TenantLifecycleAuthorizationReflectionState.Authorized, TenantLifecycleAuthorizationReflectionState.Authorized);

        public Func<string, TenantDetail, CancellationToken, ValueTask<TenantMutationAuthorizationReflection>>? MutationResolver { get; set; }

        public ValueTask<TenantMutationAuthorizationReflection> ResolveTenantMutationAuthorizationAsync(
            string tenantId, TenantDetail detail, CancellationToken cancellationToken = default)
            => MutationResolver?.Invoke(tenantId, detail, cancellationToken) ?? ValueTask.FromResult(MutationReflection);

        public ValueTask<TenantConfigurationManagementContext> ReauthorizeConfigurationManagementAsync(
            string tenantId,
            TenantStatus tenantStatus,
            TenantConfigurationSafeModel safeModel,
            CancellationToken cancellationToken = default)
            => ValueTask.FromResult(TenantConfigurationManagementContext.Unavailable(tenantId, tenantStatus));
    }

    private sealed class MutableAuthenticationStateProvider : AuthenticationStateProvider
    {
        private AuthenticationState _state = new(AdministratorPrincipal());
        private Task<AuthenticationState>? _pending;

        public override Task<AuthenticationState> GetAuthenticationStateAsync()
            => _pending?.IsFaulted == true ? Task.FromResult(_state) : _pending ?? Task.FromResult(_state);

        public void Notify(ClaimsPrincipal principal)
        {
            _state = new AuthenticationState(principal);
            _pending = null;
            NotifyAuthenticationStateChanged(Task.FromResult(_state));
        }

        public TaskCompletionSource<AuthenticationState> NotifyPending()
        {
            var pending = new TaskCompletionSource<AuthenticationState>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending = pending.Task;
            NotifyAuthenticationStateChanged(pending.Task);
            return pending;
        }
    }

    private static ClaimsPrincipal AdministratorPrincipal(string subject = "operator.alpha")
        => new(new ClaimsIdentity(
        [
            new Claim("sub", subject),
            new Claim("eventstore:tenant", "system"),
            new Claim("global_admin", "true"),
        ], "test"));

    private sealed class StubTenantsLocalizer : IStringLocalizer<TenantsResources>
    {
        // Unknown keys used to echo back as `name`, which made a missing resource indistinguishable from a
        // present one: tests rendered the literal key as user-visible copy and asserted substrings of it
        // (`"Unknown"` is a substring of `"Tenants.ProjectionLifecycle.Unknown"`), so the assertion passed
        // whether or not the string existed. Three keys added in this range were absent from Values for
        // exactly that reason and nothing failed.
        //
        // Resolution order is now: explicit override, then the real TenantsResources.resx, then throw. The
        // overrides keep the short, stable copy these tests assert on; falling through to the real resource
        // means a test can assert shipped copy without hand-copying it here; and a key that exists in
        // neither is a defect, not a silent echo.
        private static readonly ResourceManager RealResources = new(
            "Hexalith.Tenants.UI.Resources.TenantsResources",
            typeof(TenantsResources).Assembly);

        public LocalizedString this[string name] => new(name, Resolve(name));

        public LocalizedString this[string name, params object[] arguments]
            // No arguments means no substitution. Formatting unconditionally threw FormatException the
            // moment Resolve started falling through to a real .resx string containing `{0}` -- the stub
            // used to echo the placeholder-free key back, so the path could not be reached before.
            => new(name, arguments.Length == 0
                ? Resolve(name)
                : string.Format(CultureInfo.CurrentCulture, Resolve(name), arguments));

        // CurrentUICulture, not InvariantCulture. Pinning the invariant culture made this stub answer in
        // English no matter what culture a test rendered under, so a component that had hard-coded English
        // copy was indistinguishable from one that reads the localizer -- and no test could prove the French
        // resources are ever reached.
        private static string Resolve(string name)
            => Values.TryGetValue(name, out string? value)
                ? value
                : RealResources.GetString(name, CultureInfo.CurrentUICulture)
                    ?? throw new KeyNotFoundException(
                        $"Resource key '{name}' is defined neither in this stub nor in TenantsResources.resx. "
                        + "The stub must not echo an undefined key back as user-visible copy.");

        /// <summary>
        /// Enumerates everything <see cref="Resolve"/> can return, not just the overrides. Returning
        /// <c>Values</c> alone made enumeration and lookup disagree: a key resolvable through the real
        /// resource set was absent from the enumeration, so any caller reasoning about "the available
        /// strings" saw a set the indexer did not agree with. Overrides win, matching resolution order.
        /// </summary>
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
        {
            Dictionary<string, string> all = new(StringComparer.Ordinal);
            // NOT disposed: GetResourceSet returns the ResourceManager's own cached set, so disposing it
            // corrupts every subsequent lookup for the whole process -- Resolve then throws its
            // KeyNotFoundException for keys that do exist, and every bUnit WaitForAssertion downstream burns
            // its full timeout instead of failing.
            ResourceSet? real = RealResources.GetResourceSet(
                CultureInfo.CurrentUICulture,
                createIfNotExists: true,
                tryParents: includeParentCultures);
            if (real is not null)
            {
                foreach (DictionaryEntry entry in real)
                {
                    if (entry.Key is string key && entry.Value is string text)
                    {
                        all[key] = text;
                    }
                }
            }

            foreach (KeyValuePair<string, string> over in Values)
            {
                all[over.Key] = over.Value;
            }

            return all.Select(entry => new LocalizedString(entry.Key, entry.Value));
        }

        private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal)
        {
            // Without these, ProjectionLifecycleBadge rendered the literal resource key back through this
            // echoing stub, so the badge's label was never really asserted and only its CSS class was --
            // which project rules forbid relying on alone.
            ["Tenants.ProjectionLifecycle.Label"] = "Projection lifecycle",
            ["Tenants.ProjectionLifecycle.Current"] = "Current",
            ["Tenants.ProjectionLifecycle.Stale"] = "Stale",
            ["Tenants.ProjectionLifecycle.Unknown"] = "Unknown",
            ["Tenants.ProjectionLifecycle.Rebuilding"] = "Rebuilding",
            ["Tenants.ProjectionLifecycle.Degraded"] = "Degraded",
            ["Tenants.ProjectionLifecycle.Unavailable"] = "Unavailable",
            ["Tenants.ProjectionLifecycle.LocalOnly"] = "Local only",
            ["Tenants.Detail.Back"] = "Back to tenants",
            ["Tenants.Detail.Configuration.Empty"] = "No visible configuration is available in this detail projection.",
            ["Tenants.Detail.Configuration.Unavailable"] = "Configuration unavailable",
            ["Tenants.Detail.Configuration.Summary"] = "{0} visible configuration keys across {1} namespaces.",
            ["Tenants.Detail.Configuration.Title"] = "Configuration summary",
            ["Tenants.Detail.CreatedAtLabel"] = "Created",
            ["Tenants.Detail.FreshnessLabel"] = "Freshness",
            ["Tenants.Detail.FullTenantIdLabel"] = "Full tenant identifier {0}",
            ["Tenants.Detail.IdentityLabel"] = "Tenant identity",
            ["Tenants.Detail.LifecycleLabel"] = "Lifecycle",
            ["Tenants.Detail.Members.VisiblePageSummary"] = "{0} members visible on this page. Owner context is unavailable until both reads are current and version-consistent.",
            ["Tenants.Detail.Members.VisiblePageSummaryWithOwners"] = "{0} members visible on this page; authoritative tenant detail reports {1} owners.",
            ["Tenants.Detail.Members.Title"] = "Member summary",
            ["Tenants.Detail.OverviewLabel"] = "Tenant overview",
            ["Tenants.Detail.State.Degraded.Message"] = "Some tenant detail evidence is degraded. Treat the overview as incomplete until the server-side projection recovers.",
            ["Tenants.Detail.State.Degraded.Title"] = "Tenant detail is degraded",
            ["Tenants.Detail.State.Loading.Message"] = "Tenant detail is loading through the server-side query gateway.",
            ["Tenants.Detail.State.Loading.Title"] = "Loading tenant detail",
            ["Tenants.Detail.State.NotFound.Message"] = "The requested tenant was not found or is no longer visible to this operator.",
            ["Tenants.Detail.State.NotFound.Title"] = "Tenant not found",
            ["Tenants.Detail.State.Stale.Message"] = "The latest freshness evidence says this tenant detail is stale. Do not treat it as current.",
            ["Tenants.Detail.State.Stale.Title"] = "Tenant detail is stale",
            ["Tenants.Detail.State.Unauthorized.Message"] = "This operator is not authorized to view the requested tenant detail.",
            ["Tenants.Detail.State.Unauthorized.Title"] = "Tenant detail unauthorized",
            ["Tenants.Detail.State.Unavailable.Message"] = "Tenant detail cannot be loaded because the server-side query gateway is unavailable.",
            ["Tenants.Detail.State.Unavailable.Title"] = "Tenant detail unavailable",
            ["Tenants.Detail.Status.Active"] = "Active",
            ["Tenants.Detail.Status.Disabled"] = "Disabled",
            ["Tenants.Detail.Status.Unknown"] = "Unknown",
            ["Tenants.Detail.StatusAccessibleLabel"] = "Tenant status {0}",
            ["Tenants.Detail.StatusLabel"] = "Status",
            ["Tenants.Detail.Title"] = "Tenant detail",
            ["Tenants.Lifecycle.Unavailable.AlreadyActive"] = "{1} is unavailable for tenant {0} because the current projection already shows Active. If submitted by another surface, the safe domain outcome is {2}; continue read-only or refresh.",
            ["Tenants.Configuration.Announcement.Results"] = "{0} visible configuration entries across {1} namespace groups.",
            ["Tenants.Configuration.ClearFilter"] = "Clear",
            ["Tenants.Configuration.Description"] = "Read-only visible configuration from the authorized tenant detail projection.",
            ["Tenants.Configuration.Filter.Help"] = "Scan visible namespaces and literal keys. Prefix ownership is not inferred from this read model.",
            ["Tenants.Configuration.Filter.Label"] = "Filter visible configuration",
            ["Tenants.Configuration.Filter.Placeholder"] = "Namespace or key",
            ["Tenants.Configuration.GroupLabel"] = "Namespace {0}",
            ["Tenants.Configuration.Header.Key"] = "Key",
            ["Tenants.Configuration.Header.Namespace"] = "Namespace",
            ["Tenants.Configuration.Header.Value"] = "Value",
            ["Tenants.Configuration.Header.Actions"] = "Actions",
            ["Tenants.Configuration.ScopeNotice"] = "Only configuration approved by the current namespace and display policy is shown.",
            ["Tenants.Configuration.State.Loading"] = "Configuration evidence is loading.",
            ["Tenants.Configuration.State.Loading.Title"] = "Configuration loading",
            ["Tenants.Configuration.State.Ready"] = "Configuration evidence is current.",
            ["Tenants.Configuration.State.Ready.Title"] = "Configuration current",
            ["Tenants.Configuration.State.Unknown"] = "Configuration evidence cannot be verified.",
            ["Tenants.Configuration.State.Unknown.Title"] = "Configuration evidence unknown",
            ["Tenants.Configuration.State.Degraded"] = "Configuration evidence is degraded.",
            ["Tenants.Configuration.State.Empty"] = "No visible configuration is available in this tenant detail projection.",
            ["Tenants.Configuration.State.Empty.Title"] = "No visible configuration",
            ["Tenants.Configuration.State.FilteredEmpty"] = "No visible configuration matches the current namespace filter.",
            ["Tenants.Configuration.State.FilteredEmpty.Title"] = "No visible configuration matches filters",
            ["Tenants.Configuration.State.Stale"] = "Configuration evidence is stale.",
            ["Tenants.Configuration.Table.Caption"] = "Visible tenant configuration grouped by namespace",
            ["Tenants.Configuration.Table.AccessibleLabel"] = "Authorized tenant configuration values",
            ["Tenants.Configuration.Title"] = "Visible configuration",
            ["Tenants.Configuration.Management.Title"] = "Configuration management",
            ["Tenants.Configuration.Management.Description"] = "Set configuration within current authorized prefixes or remove a current safe target.",
            ["Tenants.Configuration.Management.Unavailable.Policy"] = "Configuration management is unavailable because current authorization policy cannot be verified.",
            ["Tenants.Configuration.Management.Unavailable.NoScope"] = "Configuration management is unavailable because no configuration namespace is granted to you for this tenant.",
            ["Tenants.Configuration.Management.Unavailable.ProjectionState"] = "Refresh available tenant detail before managing configuration.",
            ["Tenants.Configuration.Management.Unavailable.ProjectionLifecycle"] = "Configuration management requires a current, projection-confirmed lifecycle.",
            ["Tenants.Configuration.Management.Unavailable.Freshness"] = "Refresh current tenant detail before managing configuration.",
            ["Tenants.Configuration.Management.Unavailable.TenantLifecycle"] = "This tenant lifecycle state does not allow configuration management.",
            ["Tenants.Configuration.Management.Unavailable.CommandSurface"] = "Tenant command support is unavailable.",
            ["Tenants.Configuration.Management.Empty"] = "No current safe configuration targets are available for removal. Setting within an authorized prefix remains available.",
            ["Tenants.Configuration.Management.TargetsAccessibleLabel"] = "Configuration keys available for removal",
            ["Tenants.Configuration.Remove.Open"] = "Remove",
            ["Tenants.Configuration.Remove.OpenAccessible"] = "Remove configuration key {0}",
            ["Tenants.Configuration.Remove.Title"] = "Remove configuration",
            ["Tenants.Configuration.Remove.Description"] = "Prepare a scoped configuration removal for tenant {0} with projection confirmation.",
            ["Tenants.Configuration.Remove.Submit"] = "Confirm removal",
            ["Tenants.Configuration.Remove.Refresh"] = "Refresh status",
            ["Tenants.Configuration.Remove.Cancel"] = "Cancel",
            ["Tenants.Configuration.Remove.Confirmation.Label"] = "Type the full configuration key to confirm removal",
            ["Tenants.Configuration.Remove.Confirmation.Help"] = "Type {0} exactly. Cancel or Escape closes without submitting.",
            ["Tenants.Configuration.Remove.Lifecycle.Title"] = "Configuration removal lifecycle",
            ["Tenants.Configuration.Remove.Unavailable.Authorization"] = "You are not authorized to remove configuration for this tenant.",
            ["Tenants.Configuration.Remove.Unavailable.ProjectionState"] = "Tenant detail is unavailable or degraded. Refresh current tenant detail before removing configuration.",
            ["Tenants.Configuration.Remove.Unavailable.Freshness"] = "Refresh current tenant detail before removing configuration.",
            ["Tenants.Configuration.Remove.Unavailable.TenantLifecycle"] = "This tenant lifecycle state does not allow configuration removal.",
            ["Tenants.Configuration.Remove.Unavailable.CommandSurface"] = "Tenant command support is unavailable.",
            ["Tenants.Configuration.Remove.Unavailable.Identity"] = "Tenant identity is unavailable, so configuration removal fails closed.",
            ["Tenants.Configuration.Remove.Unavailable.Scope"] = "No authorized namespace prefix evidence is available from the current projection.",
            ["Tenants.Configuration.Remove.Unavailable.Target"] = "The selected configuration key is not visible in the current authorized projection. Refresh tenant detail before trying again.",
            ["Tenants.Configuration.Remove.Unavailable.Narrow"] = "Configuration removal is unavailable on narrow layouts because preview, tenant identity, freshness, target key, and confirmed configuration context must remain visible together.",
            ["Tenants.Configuration.Remove.Validation.KeyRequired"] = "Select a visible configuration key before previewing removal.",
            ["Tenants.Configuration.Remove.Validation.KeyVisible"] = "The selected key cannot be proven from the current authorized projection.",
            ["Tenants.Configuration.Remove.Validation.NamespaceScope"] = "The namespace prefix cannot be proven from the current authorized projection.",
            ["Tenants.Configuration.Remove.Validation.ConfirmationRequired"] = "Type {0} exactly before removing this configuration key.",
            ["Tenants.Configuration.Remove.Preview.Title"] = "Consequence preview",
            ["Tenants.Configuration.Remove.Preview.Blocked.Required"] = "Complete tenant identity, namespace, key, current state, freshness, authorization, and scope evidence before submitting.",
            ["Tenants.Configuration.Remove.Preview.Tenant"] = "Tenant",
            ["Tenants.Configuration.Remove.Preview.Namespace"] = "Namespace",
            ["Tenants.Configuration.Remove.Preview.Key"] = "Full key",
            ["Tenants.Configuration.Remove.Preview.CurrentState"] = "Current known state",
            ["Tenants.Configuration.Remove.Preview.IntendedEffect"] = "Intended effect",
            ["Tenants.Configuration.Remove.Preview.IntendedEffect.Value"] = "The selected configuration key will be removed only after command acceptance and projection proof.",
            ["Tenants.Configuration.Remove.Unavailable.TargetMissing"] = "The selected key is absent from current authorized evidence. Refresh and select a visible key; no removal was submitted.",
            ["Tenants.Configuration.Remove.Preview.FreshnessAndLifecycle.Value"] = "Read model: {0}; projection lifecycle: {1}.",
            ["Tenants.Configuration.Remove.Preview.ConsequencesAndUnknowns.Value"] = "Authorized consumers may react after projection catch-up; downstream impact and command-specific audit proof remain unknown.",
            ["Tenants.Configuration.Remove.Preview.CurrentState.Unknown"] = "Unknown",
            ["Tenants.Configuration.Remove.Preview.CurrentState.Present"] = "The key is present; its value is not displayed.",
            ["Tenants.Configuration.Remove.Preview.CurrentState.Absent"] = "The key is absent.",
            ["Tenants.Configuration.Remove.Lifecycle.Unknown"] = "Unknown",
            ["Tenants.Configuration.Remove.Lifecycle.Current"] = "Current",
            ["Tenants.Configuration.Remove.Lifecycle.Stale"] = "Stale",
            ["Tenants.Configuration.Remove.Lifecycle.Rebuilding"] = "Rebuilding",
            ["Tenants.Configuration.Remove.Lifecycle.Degraded"] = "Degraded",
            ["Tenants.Configuration.Remove.Lifecycle.Unavailable"] = "Unavailable",
            ["Tenants.Configuration.Remove.Lifecycle.LocalOnly"] = "Local only",
            ["Tenants.Configuration.Remove.Preview.Freshness"] = "Freshness evidence",
            ["Tenants.Configuration.Remove.Preview.Authorization"] = "Authorization and scope evidence",
            ["Tenants.Configuration.Remove.Preview.Authorization.Value"] = "The namespace prefix and key are visible in the authorized tenant projection; backend authorization still enforces the command.",
            ["Tenants.Configuration.Remove.Preview.KnownConsequences"] = "Known consequences versus unknowns",
            ["Tenants.Configuration.Remove.Preview.AuditExpectation"] = "Audit expectation",
            ["Tenants.Configuration.Remove.Preview.AuditExpectation.Value"] = "Projection confirmation and audit evidence remain distinct; inspect the tenant audit surface separately.",
            ["Tenants.Configuration.Remove.Preview.RecoveryPath"] = "Recovery path",
            ["Tenants.Configuration.Remove.Preview.RecoveryPath.Value"] = "Refresh tenant detail, retry only from current projection proof, or submit a forward correction to set the key again.",
            ["Tenants.Configuration.Remove.Freshness.Current"] = "Current",
            ["Tenants.Configuration.Remove.Freshness.Aging"] = "Aging",
            ["Tenants.Configuration.Remove.Freshness.Refreshing"] = "Refreshing",
            ["Tenants.Configuration.Remove.Freshness.Stale"] = "Stale",
            ["Tenants.Configuration.Remove.Freshness.Unknown"] = "Unknown",
            ["Tenants.Configuration.Remove.State.Idle"] = "No configuration removal command submitted.",
            ["Tenants.Configuration.Remove.State.Previewed"] = "Configuration removal preview ready.",
            ["Tenants.Configuration.Remove.State.RequestSent"] = "Configuration removal request sent.",
            ["Tenants.Configuration.Remove.State.Accepted"] = "Accepted by EventStore; waiting for configuration removal processing.",
            ["Tenants.Configuration.Remove.State.ProjectionPending"] = "Projection pending; the key is not confirmed removed yet.",
            ["Tenants.Configuration.Remove.State.Confirmed"] = "Projection confirmed the selected configuration key is removed.",
            ["Tenants.Configuration.Remove.State.Rejected"] = "Configuration removal command rejected.",
            ["Tenants.Configuration.Remove.State.Failed"] = "Configuration removal submission failed.",
            ["Tenants.Configuration.Remove.State.Degraded"] = "Configuration removal result is degraded and needs review.",
            ["Tenants.Configuration.Remove.State.UnableToVerify"] = "Unable to verify the configuration removal result.",
            ["Tenants.Configuration.Remove.Recovery.Idle"] = "Choose a visible key when current projection evidence and namespace scope are available.",
            ["Tenants.Configuration.Remove.Recovery.Previewed"] = "Confirm removal, cancel, or continue read-only.",
            ["Tenants.Configuration.Remove.Recovery.RequestSent"] = "Wait for command status and projection refresh.",
            ["Tenants.Configuration.Remove.Recovery.Accepted"] = "Wait, refresh status, or continue read-only until projection confirms removal.",
            ["Tenants.Configuration.Remove.Recovery.ProjectionPending"] = "Refresh tenant detail; do not display removal as complete until the key is absent from projection.",
            ["Tenants.Configuration.Remove.Recovery.Confirmed"] = "Continue read-only or inspect audit when evidence becomes available.",
            ["Tenants.Configuration.Remove.Recovery.Rejected"] = "Refresh projection evidence, request permission, start correction, or escalate.",
            ["Tenants.Configuration.Remove.Recovery.Failed"] = "Retry after checking current projection evidence or escalate.",
            ["Tenants.Configuration.Remove.Recovery.Degraded"] = "Wait, retry status lookup, inspect audit when available, or escalate.",
            ["Tenants.Configuration.Remove.Recovery.UnableToVerify"] = "Refresh, retry status lookup, continue read-only, or escalate.",
            ["Tenants.Copy.Action"] = "Copy",
            ["Tenants.Copy.Label.ConfigurationKey"] = "Copy configuration key {0}",
            ["Tenants.Copy.Label.ConfigurationValue"] = "Copy visible configuration value for {0}",
            ["Tenants.Copy.Label.TenantId"] = "Copy tenant identifier {0}",
            ["Tenants.Copy.Label.UserId"] = "Copy user identifier {0}",
            ["Tenants.Copy.Feedback.Copied"] = "Copied.",
            ["Tenants.Copy.Feedback.Disconnected"] = "Clipboard disconnected. Copy was not completed. Select the value and copy it manually.",
            ["Tenants.Copy.Feedback.Empty"] = "Nothing is available to copy.",
            ["Tenants.Copy.Feedback.Failed"] = "Copy failed. Select the value and copy it manually.",
            ["Tenants.Copy.Feedback.Unavailable"] = "Clipboard unavailable. Select the value and copy it manually.",
            ["Tenants.Copy.Feedback.Unsafe"] = "This value is not support-safe to copy.",
            ["Tenants.AddMember.Title"] = "Add tenant member",
            ["Tenants.AddMember.Description"] = "Add a literal user id to tenant {0}. Current visible owner count is {1}.",
            ["Tenants.AddMember.UserId.Label"] = "User id",
            ["Tenants.AddMember.UserId.Help"] = "Use the exact caller-supplied user id.",
            ["Tenants.AddMember.Role.Label"] = "Tenant role",
            ["Tenants.AddMember.Role.Placeholder"] = "Select a role",
            ["Tenants.AddMember.Role.TenantOwner"] = "Tenant owner",
            ["Tenants.AddMember.Role.TenantContributor"] = "Tenant contributor",
            ["Tenants.AddMember.Role.TenantReader"] = "Tenant reader",
            ["Tenants.AddMember.Submit"] = "Add member",
            ["Tenants.AddMember.Refresh"] = "Refresh status",
            ["Tenants.AddMember.Lifecycle.Title"] = "Add member command lifecycle",
            ["Tenants.AddMember.Validation.UserIdRequired"] = "User id is required.",
            ["Tenants.AddMember.Validation.RoleRequired"] = "Select TenantOwner, TenantContributor, or TenantReader before adding a member.",
            ["Tenants.AddMember.Unavailable.Authorization"] = "You are not authorized to add members to this tenant.",
            ["Tenants.AddMember.Unavailable.Freshness"] = "Refresh current tenant detail before adding a member.",
            ["Tenants.AddMember.Unavailable.TenantLifecycle"] = "This tenant lifecycle state does not allow adding members.",
            ["Tenants.AddMember.Unavailable.CommandSurface"] = "Tenant command support is unavailable.",
            ["Tenants.AddMember.Unavailable.InFlight"] = "A tenant command is already in progress.",
            ["Tenants.Members.Submit.TrackingLost"] = "This attempt can no longer be tracked, so it cannot be verified or resubmitted here. Refresh the tenant and check the member list before trying again.",
            ["Tenants.AddMember.State.Idle"] = "No add-member command submitted.",
            ["Tenants.AddMember.State.RequestSent"] = "Add-member request sent.",
            ["Tenants.AddMember.State.Accepted"] = "Accepted by EventStore; waiting for member processing.",
            ["Tenants.AddMember.State.ProjectionPending"] = "Projection pending; the member role is not confirmed visible yet.",
            ["Tenants.AddMember.State.Confirmed"] = "Projection confirmed the user is a tenant member with the requested role.",
            ["Tenants.AddMember.State.Rejected"] = "Add-member command rejected.",
            ["Tenants.AddMember.State.Failed"] = "Add-member command submission failed.",
            ["Tenants.AddMember.State.Degraded"] = "Add-member command result is degraded and needs review.",
            ["Tenants.AddMember.State.UnableToVerify"] = "Unable to verify the add-member command result.",
            ["Tenants.AddMember.Confirm.UnableToVerify.MissingProvenance"] = "Member projection already matched without provenance that this attempt advanced it. Refresh status or continue read-only.",
            ["Tenants.AddMember.Action.ContinueReadOnly"] = "Continue read-only",
            ["Tenants.ChangeRole.Title"] = "Change tenant member role",
            ["Tenants.ChangeRole.Description"] = "Change the role for user {1} in tenant {0}. The current confirmed role is {2}.",
            ["Tenants.ChangeRole.UserId.Label"] = "User id",
            ["Tenants.ChangeRole.CurrentRole.Label"] = "Current confirmed role",
            ["Tenants.ChangeRole.OwnerContext.Label"] = "Owner context",
            ["Tenants.ChangeRole.OwnerContext.NoOwners"] = "0 visible owners; owner context is unavailable.",
            ["Tenants.ChangeRole.OwnerContext.LastOwner"] = "{0} visible owner; changing this owner can leave the tenant with zero visible owners.",
            ["Tenants.ChangeRole.OwnerContext.MultipleOwners"] = "{0} visible owners.",
            ["Tenants.ChangeRole.NewRole.Label"] = "New role",
            ["Tenants.ChangeRole.NewRole.Help"] = "Select TenantOwner, TenantContributor, or TenantReader. Selecting the current role records an already applied state.",
            ["Tenants.ChangeRole.Role.TenantOwner"] = "Tenant owner",
            ["Tenants.ChangeRole.Role.TenantContributor"] = "Tenant contributor",
            ["Tenants.ChangeRole.Role.TenantReader"] = "Tenant reader",
            ["Tenants.ChangeRole.Submit"] = "Change role",
            ["Tenants.ChangeRole.Refresh"] = "Refresh status",
            ["Tenants.ChangeRole.Cancel"] = "Close",
            ["Tenants.ChangeRole.Lifecycle.Title"] = "Change role command lifecycle",
            ["Tenants.ChangeRole.Validation.RoleRequired"] = "Select TenantOwner, TenantContributor, or TenantReader before changing a role.",
            ["Tenants.ChangeRole.Unavailable.Authorization"] = "You are not authorized to change member roles in this tenant.",
            ["Tenants.ChangeRole.Unavailable.Freshness"] = "Refresh current tenant detail before changing a member role.",
            ["Tenants.ChangeRole.Unavailable.TenantLifecycle"] = "This tenant lifecycle state does not allow changing member roles.",
            ["Tenants.ChangeRole.Unavailable.CommandSurface"] = "Tenant command support is unavailable.",
            ["Tenants.ChangeRole.Unavailable.InFlight"] = "A tenant command is already in progress.",
            ["Tenants.ChangeRole.Unavailable.UnknownRole"] = "The current role is unknown, so role change fails closed until projection evidence is refreshed.",
            ["Tenants.ChangeRole.OwnerRisk.LastOwner"] = "Warning: {0} visible owner remains. This change can reduce the visible owner count to zero, but the command is not blocked solely for that reason.",
            ["Tenants.ChangeRole.AlreadyApplied.Message"] = "User {0} already has role {1}; no role-change command was submitted.",
            ["Tenants.ChangeRole.State.Idle"] = "No change-role command submitted.",
            ["Tenants.ChangeRole.State.RequestSent"] = "Change-role request sent.",
            ["Tenants.ChangeRole.State.Accepted"] = "Accepted by EventStore; waiting for member role processing.",
            ["Tenants.ChangeRole.State.ProjectionPending"] = "Projection pending; the requested role is not confirmed visible yet.",
            ["Tenants.ChangeRole.State.Confirmed"] = "Projection confirmed the target user has the requested role.",
            ["Tenants.ChangeRole.State.Rejected"] = "Change-role command rejected.",
            ["Tenants.ChangeRole.State.AlreadyApplied"] = "Already applied; the confirmed role already matches the selected role.",
            ["Tenants.ChangeRole.State.Failed"] = "Change-role command submission failed.",
            ["Tenants.ChangeRole.State.Degraded"] = "Change-role command result is degraded and needs review.",
            ["Tenants.ChangeRole.State.UnableToVerify"] = "Unable to verify the change-role command result.",
            ["Tenants.ChangeRole.Confirm.AlreadyApplied.PreExisting"] = "The requested role was already applied before this attempt; no new role change is asserted.",
            ["Tenants.ChangeRole.Confirm.UnableToVerify.MissingBaseline"] = "Role projection matched without a pre-submit baseline, so this attempt cannot be confirmed.",
            ["Tenants.ChangeRole.Confirm.UnableToVerify.MissingTarget"] = "The member projection no longer contains the target user.",
            ["Tenants.ChangeRole.Action.ContinueReadOnly"] = "Continue read-only",
            ["Tenants.Members.Action.AddMember"] = "Add member",
            ["Tenants.Members.Action.ChangeRole"] = "Change role",
            ["Tenants.Members.Action.RemoveMember"] = "Remove member",
            ["Tenants.Members.Action.Unavailable"] = "Unavailable",
            ["Tenants.RemoveMember.Title"] = "Remove tenant member",
            ["Tenants.RemoveMember.Description"] = "Preview removal of user {1} from tenant {0}. Current confirmed role is {2}.",
            ["Tenants.RemoveMember.Preview.Title"] = "Consequence preview",
            ["Tenants.RemoveMember.Preview.Tenant"] = "Tenant",
            ["Tenants.RemoveMember.Preview.TargetUser"] = "Target user",
            ["Tenants.RemoveMember.Preview.CurrentRole"] = "Current role",
            ["Tenants.RemoveMember.Preview.OwnerCount"] = "Owner count",
            ["Tenants.RemoveMember.Preview.AccessPath"] = "Affected access path",
            ["Tenants.RemoveMember.Preview.AccessPath.Value"] = "Tenant membership for the visible tenant only.",
            ["Tenants.RemoveMember.Preview.Freshness"] = "Freshness",
            ["Tenants.RemoveMember.Preview.RecoveryPath"] = "Recovery path",
            ["Tenants.RemoveMember.Preview.RecoveryPath.Value"] = "Wait, refresh, inspect audit when available, or continue read-only.",
            ["Tenants.RemoveMember.Preview.AuditExpectation"] = "Audit expectation",
            ["Tenants.RemoveMember.Preview.AuditExpectation.Value"] = "Audit evidence is requested after projection confirmation and may be pending, delayed, or unavailable.",
            ["Tenants.RemoveMember.Preview.PlatformStanding"] = "Platform standing",
            ["Tenants.RemoveMember.Preview.PlatformStanding.Known"] = "Also a global administrator. Tenant membership removal does not change platform administrator authority.",
            ["Tenants.RemoveMember.Preview.PlatformStanding.NotReflected"] = "Not reflected as a global administrator in the current complete projection.",
            ["Tenants.RemoveMember.Preview.PlatformStanding.Unknown"] = "Global-administrator standing is unproven in this view and is not guessed.",
            ["Tenants.RemoveMember.Preview.ConsequencesVersusUnknowns"] = "Known consequences versus unknowns",
            ["Tenants.RemoveMember.Preview.ConsequencesVersusUnknowns.Value"] = "Known consequence: tenant membership is removed only after projection confirmation proves the target user is absent. Known unknowns: session revocation, downstream enforcement, and token invalidation are not proven by this flow.",
            ["Tenants.RemoveMember.Preview.Blocked.Required"] = "Consequence preview is incomplete. Refresh current tenant detail before confirming removal.",
            ["Tenants.RemoveMember.Freshness.Current"] = "Current",
            ["Tenants.RemoveMember.OwnerContext.MultipleOwners"] = "{0} visible owners.",
            ["Tenants.RemoveMember.OwnerContext.LastOwner"] = "{0} visible owner; removing this member can leave zero visible owners.",
            ["Tenants.RemoveMember.OwnerContext.NoOwners"] = "0 visible owners; owner context is unavailable.",
            ["Tenants.RemoveMember.OwnerRisk.LastOwner"] = "Warning: {0} visible owner remains. Last-owner tenant membership removal is allowed, but it needs deliberate confirmation.",
            ["Tenants.RemoveMember.OwnerRisk.Accessible"] = "Elevated last-owner removal warning for {0} visible owner.",
            ["Tenants.RemoveMember.GlobalAdminRisk.Known"] = "Platform administrator authority is reflected for this user. This flow removes tenant membership only and will not remove global-administrator authority.",
            ["Tenants.RemoveMember.GlobalAdminRisk.Accessible"] = "Platform authority risk context.",
            ["Tenants.RemoveMember.Confirmation.Label"] = "Type the target user id to confirm removal",
            ["Tenants.RemoveMember.Confirmation.Elevated.Label"] = "Elevated risk: type the target user id exactly to confirm removal",
            ["Tenants.RemoveMember.Confirmation.Help"] = "Type {0} exactly. Cancel or Escape closes without submitting.",
            ["Tenants.RemoveMember.Confirmation.Elevated.Help"] = "Elevated risk: type {0} exactly. Cancel or Escape closes without submitting.",
            ["Tenants.RemoveMember.Confirm"] = "Remove member",
            ["Tenants.RemoveMember.Refresh"] = "Refresh status",
            ["Tenants.RemoveMember.Cancel"] = "Cancel",
            ["Tenants.RemoveMember.Lifecycle.Title"] = "Remove member command lifecycle",
            ["Tenants.RemoveMember.Unavailable.Narrow"] = "Removal is read-only until the browser measures a safe tablet or desktop viewport.",
            ["Tenants.RemoveMember.Unavailable.Freshness"] = "Refresh current tenant detail before removing a member.",
            ["Tenants.RemoveMember.Role.TenantOwner"] = "Tenant owner",
            ["Tenants.RemoveMember.Role.TenantContributor"] = "Tenant contributor",
            ["Tenants.RemoveMember.Role.TenantReader"] = "Tenant reader",
            ["Tenants.RemoveMember.Role.Unknown"] = "Unknown role",
            ["Tenants.RemoveMember.State.Idle"] = "No remove-member preview opened.",
            ["Tenants.RemoveMember.State.Previewed"] = "Consequence preview ready; no command has been submitted.",
            ["Tenants.RemoveMember.Recovery.Idle"] = "Open the preview when current projection evidence is available.",
            ["Tenants.RemoveMember.Recovery.Previewed"] = "Confirm deliberately, cancel, or continue read-only.",
            ["Tenants.Members.ActionSlotAccessible"] = "{0} is unavailable for {1}: {2}",
            ["Tenants.Members.Column.Actions"] = "Action availability",
            ["Tenants.Members.Column.Freshness"] = "Freshness",
            ["Tenants.Members.Column.OwnerContext"] = "Owner context",
            ["Tenants.Members.Column.Role"] = "Role",
            ["Tenants.Members.Column.Status"] = "Tenant status",
            ["Tenants.Members.Column.UserId"] = "User id",
            ["Tenants.Members.Description"] = "Read-only member access context from the dedicated authorized member projection.",
            ["Tenants.Members.Empty.Message"] = "No visible members are available. This state does not reveal hidden memberships, and actions remain unavailable until visibility and freshness are verified: {0}.",
            ["Tenants.Members.Empty.Title"] = "No visible members",
            ["Tenants.Members.Freshness.Aging"] = "Aging",
            ["Tenants.Members.Freshness.Current"] = "Current",
            ["Tenants.Members.Freshness.Refreshing"] = "Refreshing",
            ["Tenants.Members.Freshness.Stale"] = "Stale",
            ["Tenants.Members.Freshness.Unknown"] = "Unknown",
            ["Tenants.Members.OwnerContext.LastOwner"] = "{0} visible owner; last-owner changes require a later high-impact flow.",
            ["Tenants.Members.OwnerContext.MultipleOwners"] = "{0} visible owners.",
            ["Tenants.Members.OwnerContext.NoOwners"] = "0 visible owners; owner context is unavailable.",
            ["Tenants.Members.ReasonCatalogLabel"] = "Canonical unavailable action reason categories",
            ["Tenants.Members.ReasonListLabel"] = "Unavailable action reasons for {0}",
            ["Tenants.Members.Role.TenantContributor"] = "Tenant contributor",
            ["Tenants.Members.Role.TenantOwner"] = "Tenant owner",
            ["Tenants.Members.Role.TenantReader"] = "Tenant reader",
            ["Tenants.Members.Role.Unknown"] = "Unknown role",
            ["Tenants.Members.RoleAccessible"] = "Role: {0}",
            ["Tenants.Members.ScopeNotice"] = "Visible members only. Orphan context is unavailable in this read model; disabled lifecycle is shown from tenant status.",
            ["Tenants.Members.State.Degraded"] = "Member evidence is degraded.",
            ["Tenants.Members.State.Stale"] = "Member evidence is stale.",
            ["Tenants.Members.State.Loading"] = "Loading visible members.",
            ["Tenants.Members.State.Unknown"] = "Member freshness cannot be established.",
            ["Tenants.Members.State.Unauthorized"] = "You are not authorized to view these members.",
            ["Tenants.Members.State.NotFound"] = "The tenant member read was not found.",
            ["Tenants.Members.State.Invalid"] = "The member page reference is no longer valid.",
            ["Tenants.Members.State.Error"] = "The member read failed. Retry, or refresh the tenant.",
            ["Tenants.Members.State.Unavailable"] = "Visible members cannot be loaded right now.",
            ["Tenants.Members.State.Title"] = "Member read unavailable",
            ["Tenants.Members.State.Loading.Title"] = "Loading members",
            ["Tenants.Members.State.Invalid.Title"] = "Member page reference expired",
            ["Tenants.Members.State.NotFound.Title"] = "Members not found",
            ["Tenants.Members.State.Unauthorized.Title"] = "Members not available to you",
            ["Tenants.Members.Recovery.PageRecovered"] = "The member list restarted at the first page because the previous page reference expired.",
            ["Tenants.Members.OwnerContext.Unavailable"] = "Owner context is unavailable until tenant detail and member evidence are current and version-consistent.",
            ["Tenants.Members.Status.Active"] = "Active",
            ["Tenants.Members.Status.Disabled"] = "Disabled",
            ["Tenants.Members.Status.Unknown"] = "Unknown",
            ["Tenants.Members.StatusAccessible"] = "Tenant status {0}",
            ["Tenants.Members.Table.Caption"] = "Visible tenant members and read-only action availability",
            ["Tenants.Members.Title"] = "Member access review",
            ["Tenants.Members.UnavailableReason.HighImpactFlowNotReady"] = "high-impact flow not ready",
            ["Tenants.Members.UnavailableReason.AggregateLocked"] = "Another command is already in progress for this tenant. Wait for its terminal evidence or continue read-only.",
            ["Tenants.Members.UnavailableReason.MissingAuditProof"] = "missing audit proof",
            ["Tenants.Members.UnavailableReason.MissingConsequencePreview"] = "missing consequence preview",
            ["Tenants.Members.UnavailableReason.MissingLifecycleSupport"] = "missing lifecycle support",
            ["Tenants.Members.UnavailableReason.MissingPermission"] = "missing permission",
            ["Tenants.Members.UnavailableReason.StaleData"] = "stale data",
            ["Tenants.Members.UserIdAccessible"] = "Literal member user identifier {0}",
            ["Tenants.List.Column.Freshness"] = "Freshness",
            ["Tenants.List.Column.Members"] = "Members",
            ["Tenants.List.Column.Owners"] = "Owners",
            ["Tenants.List.Column.Pending"] = "Pending",
            ["Tenants.List.Column.Status"] = "Status",
            ["Tenants.List.Column.Tenant"] = "Tenant",
            ["Tenants.List.DetailLinkLabel"] = "Open tenant details for {0}",
            ["Tenants.List.Freshness.Current"] = "Current",
            ["Tenants.List.Pending.None"] = "No pending changes",
            ["Tenants.List.Pending.Unknown"] = "Pending state unknown",
            ["Tenants.List.ReturnContext"] = "Returned from tenant {0}. Filters, sort, and selection were restored on the authorized first page.",
            ["Tenants.List.Title"] = "Tenants",
            ["Tenants.Workspace.Eyebrow"] = "Tenant workspace",
        };
    }

    [Theory]
    // Each row removes exactly one clause of MemberEvidenceIsVersionConsistent / ActionsAreEvidenceBacked.
    // Every one of these survived the whole suite before: the two summary assertions matched on a prefix
    // both variants share, and nothing pinned the lifecycle or blank-version clauses at all.
    [InlineData(ProjectionLifecycleState.Current, ProjectionLifecycleState.Current, "projection-v1", "projection-v1", true)]
    [InlineData(ProjectionLifecycleState.Unknown, ProjectionLifecycleState.Current, "projection-v1", "projection-v1", false)]
    [InlineData(ProjectionLifecycleState.Current, ProjectionLifecycleState.Unknown, "projection-v1", "projection-v1", false)]
    [InlineData(ProjectionLifecycleState.Current, ProjectionLifecycleState.Current, null, null, false)]
    [InlineData(ProjectionLifecycleState.Current, ProjectionLifecycleState.Current, "  ", "  ", false)]
    [InlineData(ProjectionLifecycleState.Current, ProjectionLifecycleState.Current, "projection-v1", "projection-v2", false)]
    public void Member_governance_claims_require_current_lifecycle_and_a_stated_matching_projection_version(
        ProjectionLifecycleState detailLifecycle,
        ProjectionLifecycleState memberLifecycle,
        string? detailVersion,
        string? memberVersion,
        bool expectOwnerContext)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        TenantDetail detail = Detail(
            "tenant.alpha",
            new Dictionary<string, string> { ["billing.mode"] = "trial" },
            TenantStatus.Active,
            [
                new TenantMember("owner-user", TenantRole.TenantOwner),
                new TenantMember("reader-user", TenantRole.TenantReader),
            ]);
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(detail, detailLifecycle, detailVersion)));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantUsersSnapshot.Ready(
                "tenant.alpha",
                detail.Members,
                nextCursor: null,
                hasMore: false,
                eTag: "members-etag",
                projectionVersion: memberVersion,
                ReadModelFreshnessState.Current,
                memberLifecycle) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") }));
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-member-summary']");

        string summary = cut.Find("[data-testid='tenants-detail-member-summary']").TextContent;
        if (expectOwnerContext)
        {
            summary.ShouldContain("2 members visible on this page; authoritative tenant detail reports 1 owners.");
        }
        else
        {
            summary.ShouldContain("2 members visible on this page. Owner context is unavailable");
            summary.ShouldNotContain("reports 1 owners");
        }

        // The same evidence gate drives member action availability, so a short clause must also close the
        // change-role and remove launchers rather than only the summary wording.
        cut.FindAll("[data-testid='tenants-change-role-open']").Count.ShouldBe(expectOwnerContext ? 2 : 0);
    }

    [Fact]
    public void Member_page_with_unresolvable_freshness_is_applied_and_advances_paging()
    {
        // Unknown freshness is a successful read whose projection lifecycle could not be resolved. Excluding
        // it from IsUsableMemberPage sent the page down the degraded-retention path, which re-presented page
        // one's rows and left the cursor untouched -- so Next could never leave page one.
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        List<TenantUsersRequest> memberRequests = [];
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantUsersRequest request = call.Arg<TenantUsersRequest>()
                    ?? throw new InvalidOperationException("A tenant-users request is required.");
                memberRequests.Add(request);
                return memberRequests.Count == 1
                    ? Task.FromResult(TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-1-user", TenantRole.TenantOwner)],
                        nextCursor: "cursor-page-2",
                        hasMore: true,
                        eTag: "members-page-1",
                        projectionVersion: "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") })
                    : Task.FromResult(TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-2-user", TenantRole.TenantReader)],
                        nextCursor: null,
                        hasMore: false,
                        eTag: "members-page-2",
                        projectionVersion: "projection-v1",
                        ReadModelFreshnessState.Unknown,
                        ProjectionLifecycleState.Unknown) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), RequestCursor = request.Cursor });
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-next']").Click();

        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-member-table']")
            .TextContent.ShouldContain("page-2-user"));

        // The requested page rendered, so paging state committed: Previous is now available.
        cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeFalse();
        memberRequests.Select(static request => request.Cursor).ShouldBe([null, "cursor-page-2"]);
    }

    [Fact]
    public void Degraded_member_page_describing_another_cursor_is_not_committed_as_the_requested_page()
    {
        // The reject side of the Degraded clause in IsUsableMemberPage. A degraded snapshot retaining the
        // prior page's rows and RequestCursor must not advance _memberCursor to a page that never rendered.
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        List<TenantUsersRequest> memberRequests = [];
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantUsersRequest request = call.Arg<TenantUsersRequest>()
                    ?? throw new InvalidOperationException("A tenant-users request is required.");
                memberRequests.Add(request);
                return memberRequests.Count == 1
                    ? Task.FromResult(TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-1-user", TenantRole.TenantOwner)],
                        nextCursor: "cursor-page-2",
                        hasMore: true,
                        eTag: "members-page-1",
                        projectionVersion: "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"), RequestCursor = null })
                    // Degraded retention of page one: rows and RequestCursor still describe the PRIOR page.
                    : Task.FromResult(TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-1-user", TenantRole.TenantOwner)],
                        nextCursor: "cursor-page-2",
                        hasMore: true,
                        eTag: "members-page-1",
                        projectionVersion: "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with
                    {
                        CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                        Kind = TenantUsersSurfaceKind.Degraded,
                        RequestCursor = null,
                        Reason = TenantUsersReason.GatewayUnavailable,
                    });
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-next']").Click();

        cut.WaitForAssertion(() => memberRequests.Count.ShouldBe(2));

        // Paging state must not have advanced: Previous stays unavailable because page one is still current.
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-member-previous']")
            .All(static element => element.HasAttribute("disabled")).ShouldBeTrue());
    }

    [Fact]
    public void Member_next_is_a_no_op_when_has_more_carries_no_continuation_cursor()
    {
        // Spec rule: reject HasMore without a usable continuation cursor. No fixture in the suite paired
        // HasMore = true with a blank cursor, so the guard was never exercised.
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        List<TenantUsersRequest> memberRequests = [];
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                memberRequests.Add(call.Arg<TenantUsersRequest>()!);
                return Task.FromResult(TenantUsersSnapshot.Ready(
                    "tenant.alpha",
                    [new TenantMember("page-1-user", TenantRole.TenantOwner)],
                    nextCursor: "   ",
                    hasMore: true,
                    eTag: "members-page-1",
                    projectionVersion: "projection-v1",
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") });
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-next']").Click();

        // One read only: the initial load. The click cannot start a page read without a usable cursor.
        memberRequests.Count.ShouldBe(1);
    }

    [Fact]
    public void Authorization_safe_empty_member_page_keeps_absence_wording_at_stale_freshness()
    {
        // A successful authorized-empty page at non-Current freshness maps onto the Stale/Unknown kind,
        // which used to select the "Member read unavailable" failure copy -- reporting a read that
        // succeeded and authoritatively returned nothing as a failure.
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "trial" }, TenantStatus.Active, []),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(TenantUsersSnapshot.Empty(
                "tenant.alpha",
                isAuthorizationScoped: true,
                eTag: "members-etag",
                projectionVersion: "projection-v1",
                ReadModelFreshnessState.Stale,
                ProjectionLifecycleState.Stale) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") }));
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));

        IElement empty = cut.WaitForElement("[data-testid='tenants-member-empty']");
        empty.TextContent.ShouldContain("No visible members");

        // The freshness channel is not lost to the absence channel: both are stated.
        empty.TextContent.ShouldContain("stale");
        cut.FindAll("[data-testid='tenants-member-stale']").ShouldBeEmpty();
    }

    [Fact]
    public void Member_command_flow_survives_its_target_row_leaving_the_page_on_the_same_cursor()
    {
        // The confirmation re-query for a successful removal re-reads the SAME cursor and the target row is
        // gone. Resolving the open flow against Members.Rows unmounted it at exactly that moment, throwing
        // away the receipt and pending projection confirmation -- while the active user id survived, so
        // HasOpenMemberCommandFlow stayed true and BOTH pager buttons were disabled with no rendered flow
        // to close and no reset control. Only a full page reload recovered.
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        TenantUsersSnapshot page = TenantUsersSnapshot.Ready(
            detail.TenantId,
            detail.Members,
            nextCursor: "cursor-page-2",
            hasMore: true,
            eTag: "members-etag",
            projectionVersion: "tenant-sequence:1",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, page));

        cut.FindAll("[data-testid='tenants-remove-member-open']")[0].Click();
        cut.Find("[data-testid='tenants-remove-member-flow']");

        // Same tenant, same cursor, target row removed: the removal succeeded.
        TenantUsersSnapshot afterRemoval = page with
        {
            Rows = [.. detail.Members.Where(static member => member.UserId != "owner-user")],
        };
        cut.Render(parameters => parameters
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, afterRemoval));

        // The flow keeps rendering from its captured intent, so the receipt survives...
        cut.Find("[data-testid='tenants-remove-member-flow']");
        cut.FindAll("[data-testid='tenants-member-row']").Count.ShouldBe(1);

        // ...and closing it -- which is only possible because it is still rendered -- releases the pager.
        cut.Find("[data-testid='tenants-remove-member-cancel']").Click();
        cut.FindAll("[data-testid='tenants-remove-member-flow']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-member-next']").HasAttribute("disabled").ShouldBeFalse();
    }

    [Fact]
    public void Member_command_flow_survives_a_refresh_that_empties_the_page_entirely()
    {
        // The flow regions render outside the empty/non-empty branch for the same reason the pager does.
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));
        cut.FindAll("[data-testid='tenants-change-role-open']")[0].Click();
        cut.Find("[data-testid='tenants-change-role-flow']");

        cut.Render(parameters => parameters
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail) with { Rows = [] }));

        cut.Find("[data-testid='tenants-change-role-flow']");
    }

    [Theory]
    [InlineData("change-role", false)]
    [InlineData("remove-member", false)]
    [InlineData("change-role", true)]
    [InlineData("remove-member", true)]
    public async Task MemberPagingControlsRefuseQueuedEventsDuringRefreshAndOpenCommands(string flow, bool submitCommand)
    {
        var pendingSubmission = new TaskCompletionSource<TenantCommandSubmissionResult>(TaskCreationOptions.RunContinuationsAsynchronously);
        var gateway = new TrackingMembershipCommandGateway(pendingSubmission.Task);
        RegisterComponentServices(gateway);
        TenantDetail detail = Detail("tenant.alpha");
        int nextRequests = 0;
        int previousRequests = 0;
        TenantUsersSnapshot members = MemberSnapshot(detail) with { HasMore = true, NextCursor = "page-2" };
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.CanGoPrevious, true)
            .Add(view => view.Members, members)
            .Add(view => view.OnNextPageRequested, () => nextRequests++)
            .Add(view => view.OnPreviousPageRequested, () => previousRequests++));

        // Capture the actual event bindings while both buttons are enabled. These represent clicks
        // already dispatched before the disabled re-render; a disabled DOM click alone skips the handler.
        EventCallback<MouseEventArgs> next = MemberPagingCallback(cut, "next");
        EventCallback<MouseEventArgs> previous = MemberPagingCallback(cut, "previous");
        cut.Find("[data-testid='tenants-member-next']").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeFalse();
        cut.Render(parameters => parameters.Add(view => view.Members, members with { IsRefreshing = true }));
        await AssertBlockedAsync();

        cut.Render(parameters => parameters.Add(view => view.Members, members));
        cut.FindAll($"[data-testid='tenants-{flow}-open']").Last().Click();
        if (flow == "change-role")
        {
            FluentSelectInterop.ChangeFluentSelect(cut, "tenants-change-role-new-role", nameof(TenantRole.TenantContributor));
        }
        else
        {
            cut.Find("[data-testid='tenants-remove-member-confirmation']").Change("reader-user");
        }
        object activeFlow = flow == "change-role"
            ? cut.FindComponent<ChangeTenantMemberRoleFlow>().Instance
            : cut.FindComponent<RemoveTenantMemberFlow>().Instance;
        await AssertBlockedAsync();
        object retainedFlow = flow == "change-role"
            ? cut.FindComponent<ChangeTenantMemberRoleFlow>().Instance
            : cut.FindComponent<RemoveTenantMemberFlow>().Instance;
        retainedFlow.ShouldBeSameAs(activeFlow);
        cut.Find($"[data-testid='tenants-{flow}-flow']");
        AssertFormState();

        if (submitCommand)
        {
            Task submission = cut.Find($"[data-testid='tenants-{flow}-flow'] form").SubmitAsync();
            try
            {
                cut.WaitForAssertion(() =>
                {
                    (gateway.ChangeRoleCallCount + gateway.RemoveMemberCallCount).ShouldBe(1);
                    CommandState().State.ShouldBe(TenantCommandLifecycleState.RequestSent);
                });
                string? messageId = CommandState().MessageId;
                messageId.ShouldNotBeNullOrWhiteSpace();
                await AssertBlockedAsync();
                AssertFormState();
                CommandState().State.ShouldBe(TenantCommandLifecycleState.RequestSent);
                CommandState().MessageId.ShouldBe(messageId);
                cut.Find($"[data-testid='tenants-{flow}-cancel']").HasAttribute("disabled").ShouldBeTrue();
            }
            finally
            {
                pendingSubmission.TrySetResult(TenantCommandSubmissionResult.Rejected("Command refused by the test."));
                await submission.WaitAsync(DefaultWaitTimeout);
            }
            CommandState().State.ShouldBe(TenantCommandLifecycleState.Rejected);
        }
        gateway.ChangeRoleCallCount.ShouldBe(submitCommand && flow == "change-role" ? 1 : 0);
        gateway.RemoveMemberCallCount.ShouldBe(submitCommand && flow == "remove-member" ? 1 : 0);

        cut.Find($"[data-testid='tenants-{flow}-cancel']").Click();
        cut.FindAll($"[data-testid='tenants-{flow}-flow']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-member-next']").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeFalse();
        await cut.Find("[data-testid='tenants-member-next']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='tenants-member-previous']").ClickAsync(new MouseEventArgs());
        nextRequests.ShouldBe(1);
        previousRequests.ShouldBe(1);

        (TenantCommandLifecycleState State, string? MessageId) CommandState()
            => flow == "change-role"
                ? (cut.FindComponent<ChangeTenantMemberRoleFlow>().Instance.Snapshot.State,
                    cut.FindComponent<ChangeTenantMemberRoleFlow>().Instance.Snapshot.MessageId)
                : (cut.FindComponent<RemoveTenantMemberFlow>().Instance.Snapshot.State,
                    cut.FindComponent<RemoveTenantMemberFlow>().Instance.Snapshot.MessageId);

        void AssertFormState()
        {
            if (flow == "change-role")
            {
                cut.FindComponents<FluentSelect<string, string>>()
                    .Single(select => select.Instance.Id == "tenants-change-role-new-role")
                    .Instance.Value.ShouldBe(nameof(TenantRole.TenantContributor));
            }
            else
            {
                cut.Find("[data-testid='tenants-remove-member-confirmation']").GetAttribute("value").ShouldBe("reader-user");
            }
        }

        async Task AssertBlockedAsync()
        {
            cut.Find("[data-testid='tenants-member-next']").HasAttribute("disabled").ShouldBeTrue();
            cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeTrue();
            await cut.Find("[data-testid='tenants-member-next']").ClickAsync(new MouseEventArgs());
            await cut.Find("[data-testid='tenants-member-previous']").ClickAsync(new MouseEventArgs());
            await cut.InvokeAsync(() => next.InvokeAsync(new MouseEventArgs()));
            await cut.InvokeAsync(() => previous.InvokeAsync(new MouseEventArgs()));
            nextRequests.ShouldBe(0);
            previousRequests.ShouldBe(0);
        }
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task MemberPagingControlsHonorDirectionBoundaries(bool canGoPrevious, bool hasMore)
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        TenantUsersSnapshot members = MemberSnapshot(detail) with { HasMore = true, NextCursor = "page-2" };
        int nextRequests = 0;
        int previousRequests = 0;
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.Members, members)
            .Add(view => view.CanGoPrevious, true)
            .Add(view => view.OnNextPageRequested, () => nextRequests++)
            .Add(view => view.OnPreviousPageRequested, () => previousRequests++));
        EventCallback<MouseEventArgs> next = MemberPagingCallback(cut, "next");
        EventCallback<MouseEventArgs> previous = MemberPagingCallback(cut, "previous");

        cut.Render(parameters => parameters
            .Add(view => view.CanGoPrevious, canGoPrevious)
            .Add(view => view.Members, members with { HasMore = hasMore, NextCursor = hasMore ? "page-2" : null }));
        if (canGoPrevious || hasMore)
        {
            cut.Find("[data-testid='tenants-member-next']").HasAttribute("disabled").ShouldBe(!hasMore);
            cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBe(!canGoPrevious);
            await cut.Find("[data-testid='tenants-member-next']").ClickAsync(new MouseEventArgs());
            await cut.Find("[data-testid='tenants-member-previous']").ClickAsync(new MouseEventArgs());
        }
        else
        {
            cut.FindAll("[data-testid='tenants-member-paging']").ShouldBeEmpty();
        }
        nextRequests.ShouldBe(hasMore ? 1 : 0);
        previousRequests.ShouldBe(canGoPrevious ? 1 : 0);
        // The captured callbacks remain callable even when the pager has been removed at the boundary.
        await cut.InvokeAsync(() => next.InvokeAsync(new MouseEventArgs()));
        await cut.InvokeAsync(() => previous.InvokeAsync(new MouseEventArgs()));
        nextRequests.ShouldBe(hasMore ? 2 : 0);
        previousRequests.ShouldBe(canGoPrevious ? 2 : 0);

        cut.Render(parameters => parameters.Add(view => view.CanGoPrevious, true).Add(view => view.Members, members));
        cut.Find("[data-testid='tenants-member-next']").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeFalse();
        await cut.Find("[data-testid='tenants-member-next']").ClickAsync(new MouseEventArgs());
        await cut.Find("[data-testid='tenants-member-previous']").ClickAsync(new MouseEventArgs());
        nextRequests.ShouldBe((hasMore ? 2 : 0) + 1);
        previousRequests.ShouldBe((canGoPrevious ? 2 : 0) + 1);
    }

    [Theory]
    [InlineData("next", "page-3")]
    [InlineData("previous", null)]
    public async Task MemberPagingControlsDoNotStartAnotherReadWhileAPageIsPending(string direction, string? expectedCursor)
    {
        IRenderedComponent<TenantDetailPage> cut = await RenderDw150PageTwoAsync();
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();
        List<TenantUsersRequest> requests = [];
        var pending = new TaskCompletionSource<TenantUsersSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                requests.Add(call.Arg<TenantUsersRequest>());
                return pending.Task;
            });
        EventCallback<MouseEventArgs> next = MemberPagingCallback(cut, "next");
        EventCallback<MouseEventArgs> previous = MemberPagingCallback(cut, "previous");
        Task first = cut.Find($"[data-testid='tenants-member-{direction}']").ClickAsync(new MouseEventArgs());
        try
        {
            cut.WaitForAssertion(() =>
            {
                requests.ShouldHaveSingleItem().Cursor.ShouldBe(expectedCursor);
                AssertDw150Refreshing(cut);
            });
            await cut.InvokeAsync(() => next.InvokeAsync(new MouseEventArgs())).WaitAsync(DefaultWaitTimeout);
            await cut.InvokeAsync(() => previous.InvokeAsync(new MouseEventArgs())).WaitAsync(DefaultWaitTimeout);
            requests.ShouldHaveSingleItem().Cursor.ShouldBe(expectedCursor);
        }
        finally
        {
            pending.TrySetResult(Dw150Members("arrived-page", expectedCursor));
            await first.WaitAsync(DefaultWaitTimeout);
        }
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("arrived-page");
        cut.FindAll("[data-testid='tenants-member-refreshing']").ShouldBeEmpty();
        cut.Find("[data-testid='tenants-member-next']").HasAttribute("disabled").ShouldBeFalse();
        cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBe(expectedCursor is null);
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                requests.Add(call.Arg<TenantUsersRequest>());
                return Task.FromResult(Dw150Members("followup-page", call.Arg<TenantUsersRequest>().Cursor));
            });
        string followup = direction == "next" ? "previous" : "next";
        await cut.Find($"[data-testid='tenants-member-{followup}']").ClickAsync(new MouseEventArgs());
        requests.Select(request => request.Cursor).ShouldBe(new[] { expectedCursor, "page-2" });
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("followup-page");
    }

    private static EventCallback<MouseEventArgs> MemberPagingCallback<TComponent>(IRenderedComponent<TComponent> cut, string direction)
        where TComponent : class, IComponent
        => cut.FindComponents<FluentButton>()
            .Single(button => button.Instance.AdditionalAttributes is { } attributes
                && attributes.TryGetValue("data-testid", out object? actual)
                && Equals(actual, $"tenants-member-{direction}"))
            .Instance.OnClick;

    [Fact]
    public async Task Completed_detail_read_keeps_the_replacement_member_read_refreshing()
    {
        IRenderedComponent<TenantDetailPage> cut = await RenderDw150PageTwoAsync();
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();
        var held = HoldDw150Reads(gateway);
        Task obsolete = cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());
        cut.WaitForAssertion(() => held.Members.Count.ShouldBe(1));
        // A completed Task makes the detail await synchronous; no dispatcher-drain assumption is needed.
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(Dw150Detail("replacement")));
        Task replacement = cut.InvokeAsync(() => cut.FindComponent<EditTenantMetadataFlow>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());
        cut.WaitForAssertion(() => held.Members.Count.ShouldBe(2));
        held.Details[0].Completion.SetResult(Dw150Detail("obsolete"));
        held.Members[0].Completion.SetResult(Dw150Members("obsolete", "page-2"));
        await obsolete;
        AssertDw150Refreshing(cut);
        DetailField(cut, "_detailRefreshInFlight").ShouldBe(true);
        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain("baseline");
        held.Members[1].Completion.SetResult(Dw150Members("replacement", "page-2"));
        await replacement;
        AssertDw150Result(cut, "replacement", "replacement");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Only_the_owning_combined_cursor_recovery_can_reset_member_history(bool recoverReplacement)
    {
        IRenderedComponent<TenantDetailPage> cut = await RenderDw150PageTwoAsync();
        var oldRecoveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var currentRecoveryStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        int recoveryReads = 0;
        var held = HoldDw150Reads(Services.GetRequiredService<ITenantQueryGateway>(), onMemberRead: request =>
        {
            if (request.Cursor is null)
            {
                (Interlocked.Increment(ref recoveryReads) == 1 ? oldRecoveryStarted : currentRecoveryStarted).TrySetResult();
            }
        });
        Task obsolete = cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());
        cut.WaitForAssertion(() => held.Members.Count.ShouldBe(1));
        held.Details[0].Completion.SetResult(Dw150Detail("obsolete"));
        held.Members[0].Completion.SetResult(TenantUsersSnapshot.Invalid("tenant.alpha", TenantUsersReason.InvalidCursor));
        await oldRecoveryStarted.Task.WaitAsync(DefaultWaitTimeout);
        held.Members.Count.ShouldBe(2);
        held.Members[1].Request.Cursor.ShouldBeNull();
        Task replacement = cut.InvokeAsync(() => cut.FindComponent<EditTenantMetadataFlow>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());
        cut.WaitForAssertion(() => held.Members.Count.ShouldBe(3));
        held.Members[1].Token.IsCancellationRequested.ShouldBeTrue();
        held.Members[2].Request.Cursor.ShouldBe("page-2");
        held.Details[1].Completion.SetResult(Dw150Detail("replacement"));
        held.Members[2].Completion.SetResult(recoverReplacement
            ? TenantUsersSnapshot.Invalid("tenant.alpha", TenantUsersReason.InvalidCursor)
            : Dw150Members("replacement", "page-2"));
        if (recoverReplacement)
        {
            await currentRecoveryStarted.Task.WaitAsync(DefaultWaitTimeout);
            held.Members.Count.ShouldBe(4);
            held.Members[3].Request.TenantId.ShouldBe("tenant.alpha");
            held.Members[3].Request.Cursor.ShouldBeNull();
            held.Members[3].Request.ETag.ShouldBeNull();
            held.Members[3].Previous.ShouldNotBeNull().CallerScope
                .ShouldBe(TenantQueryGateway.AuditCallerScope("operator.alpha"));
        }
        else
        {
            await replacement;
        }
        held.Members[1].Completion.SetResult(Dw150Members("obsolete-recovery", null));
        await obsolete;
        DetailField(cut, "_memberCursor").ShouldBe("page-2");
        cut.FindAll("[data-testid='tenants-member-page-recovered']").ShouldBeEmpty();
        if (recoverReplacement)
        {
            AssertDw150Refreshing(cut);
            held.Members[3].Completion.SetResult(Dw150Members("recovered", null));
            await replacement;
            AssertDw150Result(cut, "replacement", "recovered");
            DetailField(cut, "_memberCursor").ShouldBeNull();
            cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeTrue();
            cut.FindComponent<MemberAccessReview>().Instance.Members.PagingRecovered.ShouldBeTrue();
            cut.Find("[data-testid='tenants-member-page-recovered']");
        }
        else
        {
            AssertDw150Result(cut, "replacement", "replacement");
            cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeFalse();
        }
    }

    [Theory]
    [InlineData(1, true)]
    [InlineData(1, false)]
    [InlineData(3, true)]
    [InlineData(3, false)]
    public async Task Combined_refresh_survives_detail_supersession_at_the_current_member_cursor(
        int replacements, bool obsoleteCompletesFirst)
    {
        IRenderedComponent<TenantDetailPage> cut = await RenderDw150PageTwoAsync();
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();
        var held = HoldDw150Reads(gateway);
        List<Task> operations = [cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync())];
        cut.WaitForAssertion(() => held.Members.Count.ShouldBe(1));
        // This is the missing entry point: combined refresh is held, not Next/Previous navigation.
        DetailField(cut, "_memberPageLoadInFlight").ShouldBe(false);
        for (int index = 1; index <= replacements; index++)
        {
            operations.Add(cut.InvokeAsync(() => cut.FindComponent<EditTenantMetadataFlow>().Instance
                .OnProjectionRefreshRequested.InvokeAsync()));
            cut.WaitForAssertion(() => held.Members.Count.ShouldBe(index + 1));
            held.Members[index - 1].Token.IsCancellationRequested.ShouldBeTrue();
            held.Details[index - 1].Token.IsCancellationRequested.ShouldBeTrue();
            AssertDw150Refreshing(cut);
        }

        foreach (var read in held.Members)
        {
            read.Request.TenantId.ShouldBe("tenant.alpha");
            read.Request.Cursor.ShouldBe("page-2");
            read.Request.ETag.ShouldBe("members-page-two");
            read.Previous.ShouldNotBeNull().RequestCursor.ShouldBe("page-2");
            read.Previous.CallerScope.ShouldBe(TenantQueryGateway.AuditCallerScope("operator.alpha"));
        }
        foreach (var read in held.Details)
        {
            read.Request.TenantId.ShouldBe("tenant.alpha");
            read.Request.ETag.ShouldBe("\"detail-baseline\"");
            read.Previous.ShouldNotBeNull().CallerScope.ShouldBe(TenantQueryGateway.AuditCallerScope("operator.alpha"));
        }
        held.Members[^1].Token.IsCancellationRequested.ShouldBeFalse();
        void CompleteObsolete()
        {
            for (int index = 0; index < replacements; index++)
            {
                held.Details[index].Completion.SetResult(Dw150Detail("obsolete"));
                held.Members[index].Completion.SetResult(Dw150Members("obsolete", "page-2"));
            }
        }
        if (obsoleteCompletesFirst)
        {
            CompleteObsolete();
            await Task.WhenAll(operations.Take(replacements));
            AssertDw150Refreshing(cut);
            DetailField(cut, "_detailRefreshInFlight").ShouldBe(true);
        }

        held.Details[^1].Completion.SetResult(Dw150Detail("replacement"));
        held.Members[^1].Completion.SetResult(Dw150Members("replacement", "page-2"));
        await operations[^1];
        if (!obsoleteCompletesFirst)
        {
            CompleteObsolete();
            await Task.WhenAll(operations.Take(replacements));
        }
        AssertDw150Result(cut, "replacement", "replacement");
        DetailField(cut, "_memberCursor").ShouldBe("page-2");
        DetailField(cut, "_detailRefreshInFlight").ShouldBe(false);
        cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeFalse();

        TenantDetailSnapshot appliedDetail = (TenantDetailSnapshot)DetailField(cut, "_snapshot")!;
        appliedDetail.ETag.ShouldBe("\"detail-replacement\"");
        appliedDetail.ProjectionVersion.ShouldBe("projection-replacement");
        TenantUsersSnapshot appliedMembers = cut.FindComponent<MemberAccessReview>().Instance.Members;
        appliedMembers.ETag.ShouldBe("members-replacement");
        appliedMembers.ProjectionVersion.ShouldBe("projection-replacement");
        Task followup = cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());
        cut.WaitForAssertion(() => held.Members.Count.ShouldBe(replacements + 2));
        held.Details[^1].Request.ETag.ShouldBe("\"detail-replacement\"");
        held.Members[^1].Request.ETag.ShouldBe("members-replacement");
        held.Members[^1].Request.Cursor.ShouldBe("page-2");
        held.Details[^1].Previous.ShouldNotBeNull().ProjectionVersion.ShouldBe("projection-replacement");
        held.Members[^1].Previous.ShouldNotBeNull().ProjectionVersion.ShouldBe("projection-replacement");
        held.Details[^1].Completion.SetResult(Dw150Detail("replacement"));
        held.Members[^1].Completion.SetResult(Dw150Members("replacement", "page-2"));
        await followup;

        // Exercise the retained history through Previous, not only reflection or request counts.
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Dw150Members("returned-first", call.Arg<TenantUsersRequest>().Cursor)));
        await cut.Find("[data-testid='tenants-member-previous']").ClickAsync(new MouseEventArgs());
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("returned-first");
        DetailField(cut, "_memberCursor").ShouldBeNull();
        cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeTrue();
    }

    [Theory]
    [InlineData("members-fault")]
    [InlineData("detail-fault")]
    [InlineData("both-fault")]
    [InlineData("members-denied")]
    [InlineData("wrong-caller")]
    public async Task Superseding_combined_refresh_applies_truthful_independent_failure_states(string outcome)
    {
        IRenderedComponent<TenantDetailPage> cut = await RenderDw150PageTwoAsync();
        var held = HoldDw150Reads(Services.GetRequiredService<ITenantQueryGateway>(), cooperateWithCancellation: true);
        Task obsolete = cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());
        cut.WaitForAssertion(() => held.Members.Count.ShouldBe(1));
        Task current = cut.InvokeAsync(() => cut.FindComponent<EditTenantMetadataFlow>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());
        cut.WaitForAssertion(() => held.Members.Count.ShouldBe(2));
        await obsolete;
        AssertDw150Refreshing(cut);
        DetailField(cut, "_detailRefreshInFlight").ShouldBe(true);
        if (outcome is "detail-fault" or "both-fault")
        {
            held.Details[1].Completion.SetException(new InvalidOperationException("Private backend diagnostic"));
        }
        else
        {
            held.Details[1].Completion.SetResult(Dw150Detail("replacement"));
        }
        if (outcome is "members-fault" or "both-fault")
        {
            held.Members[1].Completion.SetException(new InvalidOperationException("Private backend diagnostic"));
        }
        else
        {
            held.Members[1].Completion.SetResult(outcome switch
            {
                "members-denied" => TenantUsersSnapshot.Unauthorized("tenant.alpha"),
                "wrong-caller" => Dw150Members("other-caller-private", "page-2") with
                {
                    CallerScope = TenantQueryGateway.AuditCallerScope("operator.beta"),
                },
                _ => Dw150Members("replacement", "page-2"),
            });
        }
        await current;
        // Cancellation callbacks completed the obsolete tasks during replacement setup.
        held.Details[0].Token.IsCancellationRequested.ShouldBeTrue();
        held.Members[0].Token.IsCancellationRequested.ShouldBeTrue();

        TenantUsersSnapshot members = cut.FindComponent<MemberAccessReview>().Instance.Members;
        members.IsRefreshing.ShouldBeFalse();
        cut.FindAll("[data-testid='tenants-member-refreshing']").ShouldBeEmpty();
        DetailField(cut, "_memberCursor").ShouldBe("page-2");
        if (outcome is "members-denied")
        {
            members.Kind.ShouldBe(TenantUsersSurfaceKind.Unauthorized);
            members.Rows.ShouldBeEmpty();
            cut.FindAll("[data-testid='tenants-member-row']").ShouldBeEmpty();
        }
        else
        {
            members.Rows.ShouldHaveSingleItem().UserId.ShouldBe(outcome is "detail-fault" ? "replacement" : "page-two");
            members.Kind.ShouldBe(outcome is "detail-fault" ? TenantUsersSurfaceKind.Ready : TenantUsersSurfaceKind.Degraded);
            if (members.Kind is TenantUsersSurfaceKind.Degraded)
            {
                members.Freshness.ShouldNotBe(ReadModelFreshnessState.Current);
                members.ETag.ShouldBe("members-page-two");
            }
            cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain(members.Rows[0].UserId);
        }
        TenantDetailSnapshot detail = (TenantDetailSnapshot)DetailField(cut, "_snapshot")!;
        detail.Kind.ShouldBe(outcome is "detail-fault" or "both-fault" ? TenantDetailSurfaceKind.Degraded : TenantDetailSurfaceKind.Ready);
        if (outcome is "detail-fault" or "both-fault")
        {
            detail.Detail.ShouldNotBeNull().TenantId.ShouldBe("tenant.alpha");
            detail.ETag.ShouldBe("\"detail-baseline\"");
            detail.Freshness.ShouldBe(ReadModelFreshnessState.Unknown);
            detail.Configuration.IsAvailable.ShouldBeFalse();
            cut.Find("[data-testid='tenants-detail-degraded']");
            cut.FindAll("[data-testid='tenants-config-read-table']").ShouldBeEmpty();
        }
        else
        {
            cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain("replacement");
        }
        cut.Markup.ShouldNotContain("Private backend diagnostic");
        cut.Markup.ShouldNotContain("other-caller-private");
    }

    [Theory]
    [InlineData("tenant")]
    [InlineData("caller")]
    [InlineData("pending-caller")]
    [InlineData("signed-out")]
    [InlineData("dispose")]
    public async Task Scope_changes_cancel_combined_replacements_and_reject_late_obsolete_evidence(string transition)
    {
        IRenderedComponent<TenantDetailPage> cut = await RenderDw150PageTwoAsync();
        var held = HoldDw150Reads(Services.GetRequiredService<ITenantQueryGateway>());
        Task obsolete = cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());
        cut.WaitForAssertion(() => held.Members.Count.ShouldBe(1));
        Task replacement = cut.InvokeAsync(() => cut.FindComponent<EditTenantMetadataFlow>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());
        cut.WaitForAssertion(() => held.Members.Count.ShouldBe(2));
        var authentication = (MutableAuthenticationStateProvider)Services.GetRequiredService<AuthenticationStateProvider>();
        TaskCompletionSource<AuthenticationState>? pendingCaller = null;
        switch (transition)
        {
            case "tenant":
                cut.Render(parameters => parameters.Add(page => page.TenantId, "tenant.beta"));
                break;
            case "caller":
                await cut.InvokeAsync(() => authentication.Notify(AdministratorPrincipal("operator.beta")));
                break;
            case "pending-caller":
                await cut.InvokeAsync(() => pendingCaller = authentication.NotifyPending());
                break;
            case "signed-out":
                await cut.InvokeAsync(() => authentication.Notify(new ClaimsPrincipal(new ClaimsIdentity())));
                break;
            default:
                await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
                break;
        }
        held.Members[1].Token.IsCancellationRequested.ShouldBeTrue();
        held.Details[1].Token.IsCancellationRequested.ShouldBeTrue();
        if (transition is not "dispose")
        {
            cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-member-row']").ShouldBeEmpty());
            DetailField(cut, "_memberCursor").ShouldBeNull();
        }
        if (pendingCaller is not null)
        {
            for (int index = 0; index < 2; index++)
            {
                held.Details[index].Completion.SetResult(Dw150Detail("obsolete-private"));
                held.Members[index].Completion.SetResult(Dw150Members("obsolete-private", "page-2"));
            }
            await Task.WhenAll(obsolete, replacement);
            cut.FindAll("[data-testid='tenants-member-row']").ShouldBeEmpty();
            cut.Markup.ShouldNotContain("obsolete-private");
            DetailField(cut, "_memberCursor").ShouldBeNull();
            ((ICollection)DetailField(cut, "_memberCursorHistory")!).Count.ShouldBe(0);
            held.Members.Count.ShouldBe(2);
            pendingCaller.SetResult(new AuthenticationState(AdministratorPrincipal("operator.beta")));
        }
        if (transition is "tenant" or "caller" or "pending-caller")
        {
            cut.WaitForAssertion(() => held.Members.Count.ShouldBe(3));
            string tenantId = transition == "tenant" ? "tenant.beta" : "tenant.alpha";
            string subject = transition == "tenant" ? "operator.alpha" : "operator.beta";
            held.Members[2].Request.TenantId.ShouldBe(tenantId);
            held.Members[2].Request.Cursor.ShouldBeNull();
            held.Members[2].Request.ETag.ShouldBeNull();
            held.Members[2].Previous?.Rows.ShouldBeEmpty();
            held.Details[2].Request.TenantId.ShouldBe(tenantId);
            held.Details[2].Request.ETag.ShouldBeNull();
            held.Details[2].Completion.SetResult(Dw150Detail("new-scope", tenantId, subject));
            held.Members[2].Completion.SetResult(Dw150Members("new-scope", null, tenantId, subject));
            cut.WaitForAssertion(() => AssertDw150Result(cut, "new-scope", "new-scope"));
            cut.FindComponent<MemberAccessReview>().Instance.Members.CallerScope
                .ShouldBe(TenantQueryGateway.AuditCallerScope(subject));
        }
        // Ignore cancellation deliberately: both older generations finish after new scope results.
        for (int index = 0; index < 2; index++)
        {
            held.Details[index].Completion.TrySetResult(Dw150Detail("obsolete-private"));
            held.Members[index].Completion.TrySetResult(Dw150Members("obsolete-private", "page-2"));
        }
        await Task.WhenAll(obsolete, replacement);
        if (transition is "tenant" or "caller" or "pending-caller")
        {
            AssertDw150Result(cut, "new-scope", "new-scope");
            DetailField(cut, "_memberCursor").ShouldBeNull();
            cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeTrue();
        }
        else if (transition == "signed-out")
        {
            cut.FindAll("[data-testid='tenants-member-row']").ShouldBeEmpty();
            cut.Markup.ShouldNotContain("obsolete-private");
            ((TenantDetailSnapshot)DetailField(cut, "_snapshot")!).Kind.ShouldBe(TenantDetailSurfaceKind.Unauthorized);
        }
        else
        {
            ((TenantUsersSnapshot)DetailField(cut, "_tenantUsersSnapshot")!).Rows
                .ShouldNotContain(member => member.UserId == "obsolete-private");
        }
    }

    private async Task<IRenderedComponent<TenantDetailPage>> RenderDw150PageTwoAsync()
    {
        RegisterServices(_ => Task.FromResult(Dw150Detail("baseline")));
        ITenantQueryGateway gateway = Services.GetRequiredService<ITenantQueryGateway>();
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(Dw150Members(call.Arg<TenantUsersRequest>().Cursor is null ? "page-one" : "page-two",
                call.Arg<TenantUsersRequest>().Cursor)));
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-row']");
        await cut.Find("[data-testid='tenants-member-next']").ClickAsync(new MouseEventArgs());
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("page-two");
        return cut;
    }

    private static (
        List<(TenantDetailRequest Request, TenantDetailSnapshot? Previous, CancellationToken Token, TaskCompletionSource<TenantDetailSnapshot> Completion)> Details,
        List<(TenantUsersRequest Request, TenantUsersSnapshot? Previous, CancellationToken Token, TaskCompletionSource<TenantUsersSnapshot> Completion)> Members)
        HoldDw150Reads(ITenantQueryGateway gateway, bool cooperateWithCancellation = false, Action<TenantUsersRequest>? onMemberRead = null)
    {
        List<(TenantDetailRequest, TenantDetailSnapshot?, CancellationToken, TaskCompletionSource<TenantDetailSnapshot>)> details = [];
        List<(TenantUsersRequest, TenantUsersSnapshot?, CancellationToken, TaskCompletionSource<TenantUsersSnapshot>)> members = [];
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var completion = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                details.Add((call.Arg<TenantDetailRequest>(), call.Arg<TenantDetailSnapshot?>(), call.Arg<CancellationToken>(), completion));
                if (cooperateWithCancellation)
                {
                    CancellationToken token = call.Arg<CancellationToken>();
                    token.Register(() => completion.TrySetCanceled(token));
                }
                return completion.Task;
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                var completion = new TaskCompletionSource<TenantUsersSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
                members.Add((call.Arg<TenantUsersRequest>(), call.Arg<TenantUsersSnapshot?>(), call.Arg<CancellationToken>(), completion));
                onMemberRead?.Invoke(call.Arg<TenantUsersRequest>());
                if (cooperateWithCancellation)
                {
                    CancellationToken token = call.Arg<CancellationToken>();
                    token.Register(() => completion.TrySetCanceled(token));
                }
                return completion.Task;
            });
        return (details, members);
    }

    private static TenantDetailSnapshot Dw150Detail(string marker, string tenantId = "tenant.alpha", string subject = "operator.alpha")
    {
        TenantDetailSnapshot ready = ReadyWithSafeConfiguration(
            Detail(tenantId, new Dictionary<string, string> { ["billing.mode"] = marker }));
        return TenantDetailSnapshot.Ready(
            new TenantConfigurationComposition(ready.Detail!, ready.Configuration, ready.ConfigurationManagement),
            $"\"detail-{marker}\"", ReadModelFreshnessState.Current, ProjectionLifecycleState.Current, $"projection-{marker}")
            .WithCallerScope(TenantQueryGateway.AuditCallerScope(subject));
    }

    private static TenantUsersSnapshot Dw150Members(string userId, string? cursor, string tenantId = "tenant.alpha", string subject = "operator.alpha")
        => TenantUsersSnapshot.Ready(tenantId, [new TenantMember(userId, TenantRole.TenantOwner)],
            cursor is null ? "page-2" : "page-3", true, $"members-{userId}",
            $"projection-{userId}", ReadModelFreshnessState.Current, ProjectionLifecycleState.Current) with
        {
            RequestCursor = cursor,
            CallerScope = TenantQueryGateway.AuditCallerScope(subject),
        };

    private static void AssertDw150Refreshing(IRenderedComponent<TenantDetailPage> cut)
    {
        cut.FindComponent<MemberAccessReview>().Instance.Members.IsRefreshing.ShouldBeTrue();
        cut.Find("[data-testid='tenants-member-refreshing']");
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("page-two");
        cut.Find("[data-testid='tenants-member-next']").HasAttribute("disabled").ShouldBeTrue();
        cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeTrue();
    }

    private static void AssertDw150Result(IRenderedComponent<TenantDetailPage> cut, string detailMarker, string memberId)
    {
        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain(detailMarker);
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain(memberId);
        cut.FindComponent<MemberAccessReview>().Instance.Members.Rows.ShouldHaveSingleItem().UserId.ShouldBe(memberId);
        cut.FindComponent<MemberAccessReview>().Instance.Members.IsRefreshing.ShouldBeFalse();
        cut.FindAll("[data-testid='tenants-member-refreshing']").ShouldBeEmpty();
        cut.Markup.ShouldNotContain("obsolete");
    }

    /// <summary>
    /// A refresh arriving during a member page load must be routed through the read refresh, not silently
    /// abort it.
    /// </summary>
    /// <remarks>
    /// <c>BeginLoad</c> cancels the shared load token and clears the member in-flight flag, so a member page
    /// navigation that was in flight is aborted. The detail refresh never re-reads members, so the abort was
    /// silent: the refresh indicator vanished, the pager re-enabled (its Disabled binding reads
    /// <c>Members.IsRefreshing</c>, not the in-flight flag), and the table sat on the old page with no error
    /// and no retry, while the superseded load's <c>finally</c> could restore nothing because its generation
    /// no longer matched. Deleting the reroute survived the suite.
    /// </remarks>
    [Fact]
    public async Task A_refresh_during_a_member_page_load_reissues_the_member_read()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        List<TenantUsersRequest> memberRequests = [];
        var pendingPage = new TaskCompletionSource<TenantUsersSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantUsersRequest request = call.Arg<TenantUsersRequest>()!;
                memberRequests.Add(request);
                return memberRequests.Count switch
                {
                    1 => Task.FromResult(TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("page-1-user", TenantRole.TenantOwner)],
                        "page-2",
                        true,
                        "members-1",
                        "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") }),
                    2 => pendingPage.Task,
                    _ => Task.FromResult(TenantUsersSnapshot.Ready(
                        "tenant.alpha",
                        [new TenantMember("refreshed-user", TenantRole.TenantOwner)],
                        null,
                        false,
                        "members-refreshed",
                        "projection-v1",
                        ReadModelFreshnessState.Current,
                        ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") }),
                };
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        Task nextPage = cut.Find("[data-testid='tenants-member-next']").ClickAsync(new MouseEventArgs());
        cut.WaitForAssertion(() => memberRequests.Count.ShouldBe(2));

        // A detail refresh lands while the member page read is still open.
        // Raised through the metadata flow, whose callback is RefreshTenantDetailAsync -- the detail-only
        // path. That is the one that has to notice a member page load is open and reroute; the member
        // component's own callback already goes to the combined read.
        await cut.InvokeAsync(() => cut.FindComponent<EditTenantMetadataFlow>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());

        // The member read must be reissued, not silently dropped.
        cut.WaitForAssertion(() => memberRequests.Count.ShouldBeGreaterThanOrEqualTo(3));
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-member-table']")
            .TextContent.ShouldContain("refreshed-user"));

        pendingPage.SetResult(TenantUsersSnapshot.Unavailable("tenant.alpha"));
        await nextPage;
    }

    /// <summary>
    /// An older detail load completing after a newer one must not overwrite the newer snapshot.
    /// </summary>
    /// <remarks>
    /// The generation check and the <c>_snapshot</c> assignment are marshalled together for exactly this
    /// case. Testing and assigning on the thread-pool continuation let a newer read that completed in
    /// between lose to an older one. The thread-affinity half is not observable under bUnit's
    /// single-threaded renderer; what is observable, and what this drives, is the interleaved completion:
    /// two overlapping loads whose results arrive in the opposite order to the one they were started in.
    /// </remarks>
    [Fact]
    public async Task An_older_detail_load_completing_last_does_not_overwrite_the_newer_snapshot()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        var firstLoad = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondLoad = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        int detailReads = 0;
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Interlocked.Increment(ref detailReads) switch
            {
                1 => Task.FromResult(ReadyWithSafeConfiguration(
                    Detail("tenant.alpha"),
                    ProjectionLifecycleState.Current,
                    "projection-v1")),
                2 => firstLoad.Task,
                _ => secondLoad.Task,
            });
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>()!.TenantId))));
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");

        EventCallback refresh = cut.FindComponent<EditTenantMetadataFlow>().Instance.OnProjectionRefreshRequested;
        Task older = cut.InvokeAsync(() => refresh.InvokeAsync());
        cut.WaitForAssertion(() => detailReads.ShouldBe(2));

        Task newer = cut.InvokeAsync(() => refresh.InvokeAsync());
        cut.WaitForAssertion(() => detailReads.ShouldBe(3));

        // The NEWER read lands first...
        secondLoad.SetResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "newer" }),
            ProjectionLifecycleState.Current,
            "projection-v3"));
        await newer;
        cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-config-read-table']")
            .TextContent.ShouldContain("newer"));

        // ...and the superseded one lands afterwards. It must be discarded, not applied.
        firstLoad.SetResult(ReadyWithSafeConfiguration(
            Detail("tenant.alpha", new Dictionary<string, string> { ["billing.mode"] = "superseded" }),
            ProjectionLifecycleState.Current,
            "projection-v2"));
        await older;

        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldContain("newer");
        cut.Find("[data-testid='tenants-config-read-table']").TextContent.ShouldNotContain("superseded");
    }

    [Theory]
    [InlineData("success", "once")]
    [InlineData("empty", "once")]
    [InlineData("throw", "once")]
    [InlineData("success", "repeated")]
    [InlineData("empty", "repeated")]
    [InlineData("throw", "repeated")]
    [InlineData("success", "return")]
    [InlineData("empty", "return")]
    [InlineData("throw", "return")]
    public async Task Obsolete_subscription_completion_hands_off_to_latest_route_without_render(string completion, string route)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var setup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshedDetail = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        var refreshedMembers = new TaskCompletionSource<TenantUsersSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        IProjectionSubscription backend = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        int attempts = 0;
        bool hold = false;
        bool refreshing = false;
        string latest = route == "return" ? "tenant.alpha" : route == "repeated" ? "tenant.gamma" : "tenant.beta";
        backend.SubscribeAsync("tenants", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            call.ArgAt<CancellationToken>(2).CanBeCanceled.ShouldBeTrue();
            if (!hold) return Task.FromException(new InvalidOperationException("initial optional setup unavailable"));
            if (Interlocked.Increment(ref attempts) == 1)
            {
                return setup.Task;
            }
            // Replaceable cancellation may establish the intermediate route before its next change.
            call.ArgAt<string>(1).ShouldBeOneOf("tenant.beta", latest);
            if (call.ArgAt<string>(1) == latest) replacementEntered.TrySetResult();
            return Task.CompletedTask;
        });
        if (completion == "throw")
        {
            // A backend failure ordinarily becomes Empty. A failing optional logger exercises the
            // page's outer catch too, without substituting the real reference-counted coordinator.
            RegisterThrowingNotificationLogger();
        }
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => refreshing ? refreshedDetail.Task : Task.FromResult(ReadyWithSafeConfiguration(
                Detail(call.Arg<TenantDetailRequest>().TenantId), ProjectionLifecycleState.Current, "detail-before")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                if (refreshing)
                {
                    refreshEntered.TrySetResult();
                    return refreshedMembers.Task;
                }
                return Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>().TenantId)));
            });
        RegisterHandoffServices(gateway, backend, notifier);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        await cut.InvokeAsync(() => Task.CompletedTask);
        hold = true;
        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance.OnProjectionRefreshRequested.InvokeAsync());
        // The held owner is OnAfterRender, whose completion has no automatic render.
        attempts.ShouldBe(1);
        backend.ClearReceivedCalls(); // Exclude cleanup from the initial unavailable-service phase.
        var callbacks = (IDictionary)typeof(TenantReadRefreshSubscription).GetField("_callbacks", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(Services.GetRequiredService<TenantReadRefreshSubscription>())!;
        Func<Task> obsoleteCallback = ((IDictionary)callbacks.Values.Cast<object>().Single()).Values.Cast<Func<Task>>().Single();
        cut.Render(p => p.Add(page => page.TenantId, "tenant.beta"));
        if (route != "once")
        {
            cut.Render(p => p.Add(page => page.TenantId, latest));
        }
        cut.WaitForAssertion(() => HandoffField<TenantDetailSnapshot>(cut, "_snapshot").Detail!.TenantId.ShouldBe(latest));
        int before = HandoffReadCount(gateway);
        await cut.InvokeAsync(() => notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.alpha"));
        HandoffReadCount(gateway).ShouldBe(before, "obsolete callbacks must not issue even one read");
        if (completion == "success") setup.SetResult();
        else setup.SetException(new InvalidOperationException("private transport diagnostics"));
        // No render/parameter update after releasing setup: its own completion must wake the latest route.
        await replacementEntered.Task.WaitAsync(BunitContext.DefaultWaitTimeout);
        await WaitForHandoffIdleAsync(cut);
        HandoffField<string>(cut, "_subscriptionTenantId").ShouldBe(latest);
        HandoffField<IAsyncDisposable>(cut, "_readRefreshLease").ShouldNotBeNull();
        attempts.ShouldBe(route == "once" ? 2 : 3);
        callbacks.Count.ShouldBe(1);
        ((IDictionary)callbacks.Values.Cast<object>().Single()).Count.ShouldBe(1);
        await AssertObsoleteHandoffCallbackAsync(cut, gateway, obsoleteCallback);
        HandoffField<int>(cut, "_readRefreshAttempts").ShouldBe(0, "obsolete failure must not charge the current tenant");
        cut.Markup.ShouldNotContain("private transport diagnostics");
        before = HandoffReadCount(gateway);
        await cut.InvokeAsync(() =>
        {
            notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("other-projection", latest);
            notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.obsolete");
        });
        HandoffReadCount(gateway).ShouldBe(before);
        refreshing = true;
        await cut.InvokeAsync(() => notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", latest));
        await refreshEntered.Task.WaitAsync(BunitContext.DefaultWaitTimeout);
        TenantUsersSnapshot retained = HandoffField<TenantUsersSnapshot>(cut, "_tenantUsersSnapshot");
        retained.IsRefreshing.ShouldBeTrue();
        retained.ProjectionVersion.ShouldBe("tenant-sequence:1");
        HandoffField<TenantDetailSnapshot>(cut, "_snapshot").ProjectionVersion.ShouldBe("detail-before");
        TenantDetailRequest request = gateway.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(ITenantQueryGateway.GetTenantAsync))
            .Last().GetArguments()[0].ShouldBeOfType<TenantDetailRequest>();
        request.TenantId.ShouldBe(latest);
        request.ETag.ShouldBe("\"etag\"");
        TenantUsersRequest memberRequest = gateway.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(ITenantQueryGateway.GetTenantUsersAsync))
            .Last().GetArguments()[0].ShouldBeOfType<TenantUsersRequest>();
        memberRequest.TenantId.ShouldBe(latest);
        memberRequest.ETag.ShouldBe("members-etag");
        refreshedDetail.SetResult(ReadyWithSafeConfiguration(Detail(latest, new Dictionary<string, string> { ["billing.mode"] = "current-value" }), ProjectionLifecycleState.Current, "detail-after"));
        refreshedMembers.SetResult(MemberSnapshot(Detail(latest, new Dictionary<string, string>(), TenantStatus.Active, [new TenantMember("current-user", TenantRole.TenantReader)])) with { ProjectionVersion = "members-after" });
        cut.WaitForAssertion(() =>
        {
            HandoffField<TenantDetailSnapshot>(cut, "_snapshot").ProjectionVersion.ShouldBe("detail-after");
            HandoffField<TenantUsersSnapshot>(cut, "_tenantUsersSnapshot").ProjectionVersion.ShouldBe("members-after");
            HandoffField<TenantUsersSnapshot>(cut, "_tenantUsersSnapshot").IsRefreshing.ShouldBeFalse();
            cut.Markup.ShouldContain("current-user");
            cut.Markup.ShouldContain("current-value");
        });
        for (int i = 0; i < 3; i++) cut.Render();
        attempts.ShouldBe(route == "once" ? 2 : 3, "ordinary renders must retain one established subscription");
        await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        int expectedReleases = latest == "tenant.alpha" ? 2 : 1;
        await backend.Received(expectedReleases).UnsubscribeAsync("tenants", latest, Arg.Any<CancellationToken>());
        before = HandoffReadCount(gateway);
        await cut.InvokeAsync(() => notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", latest));
        HandoffReadCount(gateway).ShouldBe(before);
    }

    [Theory]
    [InlineData("pending", "success")]
    [InlineData("pending", "empty")]
    [InlineData("pending", "throw")]
    [InlineData("changed", "success")]
    [InlineData("changed", "empty")]
    [InlineData("changed", "throw")]
    [InlineData("returned", "success")]
    [InlineData("returned", "empty")]
    [InlineData("returned", "throw")]
    [InlineData("signed-out", "success")]
    [InlineData("signed-out", "empty")]
    [InlineData("signed-out", "throw")]
    [InlineData("invalid-route", "success")]
    [InlineData("invalid-route", "empty")]
    [InlineData("invalid-route", "throw")]
    [InlineData("disposed", "success")]
    [InlineData("disposed", "empty")]
    [InlineData("disposed", "throw")]
    public async Task Held_subscription_completion_respects_current_caller_route_and_disposal(string transition, string completion)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var authentication = new MutableAuthenticationStateProvider();
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        var setup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        IProjectionSubscription backend = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        bool hold = false;
        int attempts = 0;
        string caller = "operator.alpha";
        backend.SubscribeAsync("tenants", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (!hold) return Task.FromException(new InvalidOperationException("initial setup unavailable"));
            if (Interlocked.Increment(ref attempts) == 1) return setup.Task;
            replacementEntered.TrySetResult();
            return Task.CompletedTask;
        });
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ReadyWithSafeConfiguration(Detail(call.Arg<TenantDetailRequest>().TenantId),
                ProjectionLifecycleState.Current, "detail-" + caller).WithCallerScope(TenantQueryGateway.AuditCallerScope(caller))));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>().TenantId)) with
                { CallerScope = TenantQueryGateway.AuditCallerScope(caller), ProjectionVersion = "members-" + caller }));
        if (completion == "throw") RegisterThrowingNotificationLogger();
        RegisterHandoffServices(gateway, backend, notifier);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        await cut.InvokeAsync(() => Task.CompletedTask);
        hold = true;
        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance.OnProjectionRefreshRequested.InvokeAsync());
        attempts.ShouldBe(1);
        backend.ClearReceivedCalls();
        int releases = transition is "changed" or "pending" or "returned" ? 2 : 1;
        int releaseCount = 0;
        var cleanupCompleted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        backend.UnsubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (Interlocked.Increment(ref releaseCount) == releases) cleanupCompleted.TrySetResult();
            return Task.CompletedTask;
        });
        var callbacks = (IDictionary)typeof(TenantReadRefreshSubscription).GetField("_callbacks", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(Services.GetRequiredService<TenantReadRefreshSubscription>())!;
        Func<Task> obsoleteCallback = ((IDictionary)callbacks.Values.Cast<object>().Single()).Values.Cast<Func<Task>>().Single();
        TaskCompletionSource<AuthenticationState>? pending = null;
        if (transition == "pending") await cut.InvokeAsync(() => pending = authentication.NotifyPending());
        else if (transition is "changed" or "returned")
        {
            caller = "operator.beta";
            await cut.InvokeAsync(() => authentication.Notify(AdministratorPrincipal(caller)));
            cut.WaitForAssertion(() => HandoffField<TenantDetailSnapshot>(cut, "_snapshot").CallerScope.ShouldBe(TenantQueryGateway.AuditCallerScope(caller)));
        }
        if (transition == "returned")
        {
            caller = "operator.alpha";
            await cut.InvokeAsync(() => authentication.Notify(AdministratorPrincipal(caller)));
            cut.WaitForAssertion(() => HandoffField<TenantDetailSnapshot>(cut, "_snapshot").CallerScope.ShouldBe(TenantQueryGateway.AuditCallerScope(caller)));
        }
        else if (transition == "signed-out") await cut.InvokeAsync(() => authentication.Notify(new ClaimsPrincipal(new ClaimsIdentity())));
        else if (transition == "invalid-route") cut.Render(p => p.Add(page => page.TenantId, " "));
        else if (transition == "disposed") await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        int reads = HandoffReadCount(gateway);
        await cut.InvokeAsync(() => notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.alpha"));
        HandoffReadCount(gateway).ShouldBe(reads, "the original callback is invalid immediately on transition");
        if (completion == "success") setup.SetResult();
        else setup.SetException(new InvalidOperationException("private transport diagnostics"));
        // Await the actual lifecycle task's completion flag; no manufactured render is used to wake setup.
        await WaitForHandoffIdleAsync(cut);
        if (transition == "pending")
        {
            attempts.ShouldBe(1);
            HandoffField<IAsyncDisposable?>(cut, "_readRefreshLease").ShouldBeNull();
            caller = "operator.beta";
            pending!.SetResult(new AuthenticationState(AdministratorPrincipal(caller)));
            await replacementEntered.Task.WaitAsync(BunitContext.DefaultWaitTimeout);
        }
        if (transition is "changed" or "pending" or "returned")
        {
            await replacementEntered.Task.WaitAsync(BunitContext.DefaultWaitTimeout);
            await WaitForHandoffIdleAsync(cut);
            HandoffField<IAsyncDisposable?>(cut, "_readRefreshLease").ShouldNotBeNull();
            HandoffField<TenantDetailSnapshot>(cut, "_snapshot").CallerScope.ShouldBe(TenantQueryGateway.AuditCallerScope(caller));
            HandoffField<int>(cut, "_readRefreshAttempts").ShouldBe(0);
            await AssertObsoleteHandoffCallbackAsync(cut, gateway, obsoleteCallback);
            reads = gateway.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ITenantQueryGateway.GetTenantAsync));
            await cut.InvokeAsync(() => notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.alpha"));
            cut.WaitForAssertion(() => gateway.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ITenantQueryGateway.GetTenantAsync)).ShouldBe(reads + 1));
            HandoffField<TenantUsersSnapshot>(cut, "_tenantUsersSnapshot").ProjectionVersion.ShouldBe("members-" + caller);
            await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        }
        else
        {
            attempts.ShouldBe(1);
            HandoffField<IAsyncDisposable?>(cut, "_readRefreshLease").ShouldBeNull();
            await cut.InvokeAsync(() => notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.alpha"));
            HandoffReadCount(gateway).ShouldBe(reads);
        }
        // Setup cancellation releases the page gate before an ignoring provider finishes cleanup.
        await cleanupCompleted.Task.WaitAsync(BunitContext.DefaultWaitTimeout);
        await backend.Received(releases).UnsubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>());
        callbacks.Count.ShouldBe(0, "neither terminal completion nor page disposal may leave a registered callback");
    }

    private static async Task WaitForHandoffIdleAsync(IRenderedComponent<TenantDetailPage> cut)
    {
        using var bound = new CancellationTokenSource(BunitContext.DefaultWaitTimeout);
        while (HandoffField<int>(cut, "_readRefreshSubscriptionInFlight") != 0)
        {
            await Task.Delay(1, bound.Token);
        }
        await cut.InvokeAsync(() => Task.CompletedTask);
    }

    [Fact]
    public async Task Handoff_preserves_current_failure_budget_and_explicit_recovery()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var held = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacement = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backend = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        bool hold = false;
        int alpha = 0;
        int beta = 0;
        backend.SubscribeAsync("tenants", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (call.ArgAt<string>(1) == "tenant.beta")
            {
                replacement.TrySetResult();
                return Interlocked.Increment(ref beta) <= 3
                    ? Task.FromException(new InvalidOperationException("current subscription unavailable")) : Task.CompletedTask;
            }
            if (!hold) return Task.FromException(new InvalidOperationException("initial setup unavailable"));
            Interlocked.Increment(ref alpha);
            return held.Task;
        });
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ReadyWithSafeConfiguration(Detail(call.Arg<TenantDetailRequest>().TenantId),
                ProjectionLifecycleState.Current, "detail-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>().TenantId))));
        RegisterHandoffServices(gateway, backend, notifier);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        hold = true;
        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance.OnProjectionRefreshRequested.InvokeAsync());
        alpha.ShouldBe(1);
        cut.Render(p => p.Add(page => page.TenantId, "tenant.beta"));
        held.SetException(new InvalidOperationException("obsolete subscription unavailable"));
        await replacement.Task.WaitAsync(BunitContext.DefaultWaitTimeout);
        await WaitForHandoffIdleAsync(cut);
        beta.ShouldBe(1, "handoff wakes exactly one current attempt, not an eager retry loop");
        for (int i = 0; i < 12; i++) cut.Render();
        beta.ShouldBe(3);
        HandoffField<int>(cut, "_readRefreshAttempts").ShouldBe(3);
        HandoffField<IAsyncDisposable?>(cut, "_readRefreshLease").ShouldBeNull();
        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance.OnProjectionRefreshRequested.InvokeAsync());
        HandoffField<IAsyncDisposable?>(cut, "_readRefreshLease").ShouldNotBeNull();
        beta.ShouldBe(4);
        int reads = gateway.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ITenantQueryGateway.GetTenantAsync));
        await cut.InvokeAsync(() => notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.beta"));
        gateway.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ITenantQueryGateway.GetTenantAsync)).ShouldBe(reads + 1);
        HandoffField<TenantUsersSnapshot>(cut, "_tenantUsersSnapshot").IsRefreshing.ShouldBeFalse();
        HandoffField<TenantDetailSnapshot>(cut, "_snapshot").Detail!.TenantId.ShouldBe("tenant.beta");
    }

    [Fact]
    public async Task Caller_invalidation_releases_only_the_pages_shared_refresh_lease()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var authentication = new MutableAuthenticationStateProvider();
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backend = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"))));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(Detail("tenant.alpha"))));
        RegisterHandoffServices(gateway, backend, notifier);
        int independentReads = 0;
        TenantReadRefreshSubscription coordinator = Services.GetRequiredService<TenantReadRefreshSubscription>();
        await using TenantReadRefreshLease other = await coordinator.SubscribeAsync("tenants", "tenant.alpha", () =>
        {
            Interlocked.Increment(ref independentReads);
            return Task.CompletedTask;
        });
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        HandoffField<IAsyncDisposable?>(cut, "_readRefreshLease").ShouldNotBeNull();
        await cut.InvokeAsync(() => authentication.Notify(new ClaimsPrincipal(new ClaimsIdentity())));
        HandoffField<IAsyncDisposable?>(cut, "_readRefreshLease").ShouldBeNull();
        int reads = HandoffReadCount(gateway);
        await cut.InvokeAsync(() => notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.alpha"));
        independentReads.ShouldBe(1);
        HandoffReadCount(gateway).ShouldBe(reads);
        var callbacks = (IDictionary)typeof(TenantReadRefreshSubscription).GetField("_callbacks", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(coordinator)!;
        ((IDictionary)callbacks.Values.Cast<object>().Single()).Count.ShouldBe(1);
        await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        await backend.DidNotReceive().UnsubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>());
        await other.DisposeAsync();
        await backend.Received(1).UnsubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>());
        callbacks.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Held_setup_survives_same_route_reads_or_hands_off_through_two_obsolete_owners(bool successiveRoutes)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var first = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var second = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var secondEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finalEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backend = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        bool hold = false;
        int attempts = 0;
        backend.SubscribeAsync("tenants", Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (!hold) return Task.FromException(new InvalidOperationException("initial setup unavailable"));
            int attempt = Interlocked.Increment(ref attempts);
            if (attempt == 1) return first.Task;
            if (attempt == 2) { secondEntered.TrySetResult(); return second.Task; }
            finalEntered.TrySetResult();
            return Task.CompletedTask;
        });
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ReadyWithSafeConfiguration(Detail(call.Arg<TenantDetailRequest>().TenantId),
                ProjectionLifecycleState.Current, "detail-confirmed")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>().TenantId,
                new Dictionary<string, string>(), TenantStatus.Active, [new TenantMember("confirmed-user", TenantRole.TenantReader)])) with
                { NextCursor = "page-2", HasMore = true, RequestCursor = call.Arg<TenantUsersRequest>().Cursor }));
        RegisterHandoffServices(gateway, backend, notifier);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        hold = true;
        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance.OnProjectionRefreshRequested.InvokeAsync());
        backend.ClearReceivedCalls();
        string latest = successiveRoutes ? "tenant.gamma" : "tenant.alpha";
        if (successiveRoutes)
        {
            cut.Render(p => p.Add(page => page.TenantId, "tenant.beta"));
            first.SetResult();
            await secondEntered.Task.WaitAsync(BunitContext.DefaultWaitTimeout);
            cut.Render(p => p.Add(page => page.TenantId, latest));
            await finalEntered.Task.WaitAsync(BunitContext.DefaultWaitTimeout);
            attempts.ShouldBe(3);
            second.SetResult();
        }
        else
        {
            long epoch = HandoffField<long>(cut, "_readRefreshRouteVersion");
            await cut.Find("[data-testid='tenants-member-next']").ClickAsync(new MouseEventArgs());
            HandoffField<string>(cut, "_memberCursor").ShouldBe("page-2");
            await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance.OnProjectionRefreshRequested.InvokeAsync());
            await cut.InvokeAsync(() => cut.FindComponent<EditTenantMetadataFlow>().Instance.OnProjectionRefreshRequested.InvokeAsync());
            HandoffField<long>(cut, "_readRefreshRouteVersion").ShouldBe(epoch);
            attempts.ShouldBe(1);
            first.SetResult();
        }
        await WaitForHandoffIdleAsync(cut);
        HandoffField<string>(cut, "_subscriptionTenantId").ShouldBe(latest);
        HandoffField<IAsyncDisposable?>(cut, "_readRefreshLease").ShouldNotBeNull();
        int reads = gateway.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ITenantQueryGateway.GetTenantAsync));
        await cut.InvokeAsync(() => notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", latest));
        gateway.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ITenantQueryGateway.GetTenantAsync)).ShouldBe(reads + 1);
        HandoffField<TenantUsersSnapshot>(cut, "_tenantUsersSnapshot").TenantId.ShouldBe(latest);
        cut.Find("[data-testid='tenants-member-table']").TextContent.ShouldContain("confirmed-user");
        HandoffField<TenantUsersSnapshot>(cut, "_tenantUsersSnapshot").IsRefreshing.ShouldBeFalse();
        attempts.ShouldBe(successiveRoutes ? 3 : 1);
        await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        foreach (string tenant in successiveRoutes ? new[] { "tenant.alpha", "tenant.beta", "tenant.gamma" } : new[] { "tenant.alpha" })
        {
            await backend.Received(1).UnsubscribeAsync("tenants", tenant, Arg.Any<CancellationToken>());
        }
    }

    [Fact]
    public async Task Held_caller_cleanup_cannot_retain_private_rows_or_detach_the_replacement_lease()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var authentication = new MutableAuthenticationStateProvider();
        Services.AddSingleton<AuthenticationStateProvider>(authentication);
        var cleanup = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var cleanupEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var replacementEntered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var currentDetail = new TaskCompletionSource<TenantDetailSnapshot>(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backend = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        string caller = "operator.alpha";
        int attempts = 0;
        backend.SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (Interlocked.Increment(ref attempts) == 2) replacementEntered.TrySetResult();
            return Task.CompletedTask;
        });
        int releases = 0;
        backend.UnsubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>()).Returns(_ =>
        {
            if (Interlocked.Increment(ref releases) != 1) return Task.CompletedTask;
            cleanupEntered.TrySetResult();
            return cleanup.Task;
        });
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => caller == "operator.beta" ? currentDetail.Task : Task.FromResult(ReadyWithSafeConfiguration(Detail("tenant.alpha"))));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromResult(MemberSnapshot(Detail("tenant.alpha", new Dictionary<string, string>(), TenantStatus.Active,
                [new TenantMember(caller == "operator.alpha" ? "private-old-user" : "new-caller-user", TenantRole.TenantReader)])) with
                { CallerScope = TenantQueryGateway.AuditCallerScope(caller) }));
        RegisterHandoffServices(gateway, backend, notifier);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-table']");
        var callbacks = (IDictionary)typeof(TenantReadRefreshSubscription).GetField("_callbacks", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(Services.GetRequiredService<TenantReadRefreshSubscription>())!;
        Func<Task> obsoleteCallback = ((IDictionary)callbacks.Values.Cast<object>().Single()).Values.Cast<Func<Task>>().Single();
        caller = "operator.beta";
        await cut.InvokeAsync(() => authentication.Notify(AdministratorPrincipal(caller)));
        await cleanupEntered.Task.WaitAsync(BunitContext.DefaultWaitTimeout);
        cut.Markup.ShouldNotContain("private-old-user");
        HandoffField<IAsyncDisposable?>(cut, "_readRefreshLease").ShouldBeNull();
        gateway.ReceivedCalls().Count(c => c.GetMethodInfo().Name == nameof(ITenantQueryGateway.GetTenantAsync)).ShouldBe(2,
            "current reads must proceed while unsubscribe is held");
        await AssertObsoleteHandoffCallbackAsync(cut, gateway, obsoleteCallback);
        currentDetail.SetResult(ReadyWithSafeConfiguration(Detail("tenant.alpha")).WithCallerScope(TenantQueryGateway.AuditCallerScope(caller)));
        cut.WaitForAssertion(() => cut.Markup.ShouldContain("new-caller-user"));
        cleanup.SetResult();
        await replacementEntered.Task.WaitAsync(BunitContext.DefaultWaitTimeout);
        await WaitForHandoffIdleAsync(cut);
        IAsyncDisposable replacementLease = HandoffField<IAsyncDisposable>(cut, "_readRefreshLease");
        await AssertObsoleteHandoffCallbackAsync(cut, gateway, obsoleteCallback);
        int reads = HandoffReadCount(gateway);
        await cut.InvokeAsync(() => notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.alpha"));
        HandoffReadCount(gateway).ShouldBeGreaterThan(reads);
        HandoffField<IAsyncDisposable>(cut, "_readRefreshLease").ShouldBeSameAs(replacementLease);
        ((IDictionary)callbacks.Values.Cast<object>().Single()).Count.ShouldBe(1);
        await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        releases.ShouldBe(2);
        callbacks.Count.ShouldBe(0);
    }

    [Theory]
    [InlineData("cooperative")]
    [InlineData("late-success")]
    [InlineData("late-failure")]
    public async Task Finite_subscription_deadline_releases_detail_gate_and_explicit_refresh_recovers(string outcome)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        var clock = new ManualTimeProvider();
        Services.AddSingleton<TimeProvider>(clock);
        var configuration = new Microsoft.Extensions.Configuration.ConfigurationManager();
        TimeSpan configuredBound = TimeSpan.FromMilliseconds(250);
        configuration[TenantUiOperationPolicy.ConfigurationKey] = configuredBound.ToString();
        Services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(configuration);
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backend = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        var pending = new List<TaskCompletionSource>();
        var tokens = new List<CancellationToken>();
        bool hold = false;
        bool recover = false;
        backend.SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>()).Returns(call =>
        {
            if (recover) return Task.CompletedTask;
            if (!hold) return Task.FromException(new InvalidOperationException("initial setup unavailable"));
            if (outcome == "cooperative" && tokens.Count > 0)
                return Task.FromException(new InvalidOperationException("later optional setup unavailable"));
            var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            pending.Add(completion);
            CancellationToken token = call.ArgAt<CancellationToken>(2);
            tokens.Add(token);
            return outcome == "cooperative" ? completion.Task.WaitAsync(token) : completion.Task;
        });
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ReadyWithSafeConfiguration(Detail(call.Arg<TenantDetailRequest>().TenantId),
                ProjectionLifecycleState.Current, "detail-confirmed")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>().TenantId))));
        RegisterHandoffServices(gateway, backend, notifier);
        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(p => p.Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        await WaitForHandoffIdleAsync(cut);
        hold = true;
        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance.OnProjectionRefreshRequested.InvokeAsync());
        cut.WaitForAssertion(() => pending.Count.ShouldBe(1));
        var callbacks = (IDictionary)typeof(TenantReadRefreshSubscription).GetField("_callbacks", BindingFlags.Instance | BindingFlags.NonPublic)!
            .GetValue(Services.GetRequiredService<TenantReadRefreshSubscription>())!;
        Func<Task> obsoleteCallback = ((IDictionary)callbacks.Values.Cast<object>().Single()).Values.Cast<Func<Task>>().Single();
        // Completion can render and start the next bounded attempt before the old gate's idle
        // moment is observed. Drive virtual deadlines until the exact three-failure budget is
        // exhausted, supplying a render only when no setup already owns the page gate.
        CancellationTokenSource? lastAdvanced = null;
        int deadlinesAdvanced = 0;
        using (var bound = new CancellationTokenSource(BunitContext.DefaultWaitTimeout))
        {
            while (HandoffField<int>(cut, "_readRefreshAttempts") < 3
                || HandoffField<int>(cut, "_readRefreshSubscriptionInFlight") != 0)
            {
                await cut.InvokeAsync(() =>
                {
                    if (HandoffField<int>(cut, "_readRefreshSubscriptionInFlight") == 0
                        && HandoffField<int>(cut, "_readRefreshAttempts") < 3) cut.Render();
                    CancellationTokenSource? current = HandoffField<CancellationTokenSource?>(cut, "_readRefreshSetupCancellation");
                    if (current is not null && !ReferenceEquals(current, lastAdvanced) && clock.PendingTimerCount > 0)
                    {
                        // Exactly one configured period per owner; no extra virtual time can hide
                        // an attempt that missed its deadline while its continuation settles.
                        lastAdvanced = current;
                        deadlinesAdvanced++;
                        clock.Advance(configuredBound);
                    }
                });
                await Task.Delay(1, bound.Token);
            }
        }
        HandoffField<int>(cut, "_readRefreshAttempts").ShouldBe(3);
        deadlinesAdvanced.ShouldBe(outcome == "cooperative" ? 1 : 3);
        clock.GetUtcNow().ShouldBe(DateTimeOffset.UnixEpoch.AddTicks(configuredBound.Ticks * deadlinesAdvanced));
        HandoffField<IAsyncDisposable?>(cut, "_readRefreshLease").ShouldBeNull();
        HandoffField<TenantDetailSnapshot>(cut, "_snapshot").ProjectionVersion.ShouldBe("detail-confirmed");
        callbacks.Count.ShouldBe(0);
        tokens.ShouldAllBe(token => token.IsCancellationRequested);
        int exhaustedCalls = backend.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IProjectionSubscription.SubscribeAsync));
        cut.Render();
        backend.ReceivedCalls().Count(call => call.GetMethodInfo().Name == nameof(IProjectionSubscription.SubscribeAsync)).ShouldBe(exhaustedCalls);
        await AssertObsoleteHandoffCallbackAsync(cut, gateway, obsoleteCallback);
        foreach (TaskCompletionSource completion in pending)
        {
            if (outcome == "late-failure") completion.TrySetException(new InvalidOperationException("private late provider failure"));
            else completion.TrySetResult();
        }
        recover = true;
        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance.OnProjectionRefreshRequested.InvokeAsync());
        await WaitForHandoffIdleAsync(cut);
        HandoffField<string>(cut, "_subscriptionTenantId").ShouldBe("tenant.alpha");
        ((IDictionary)callbacks.Values.Cast<object>().Single()).Count.ShouldBe(1);
        int reads = HandoffReadCount(gateway);
        await cut.InvokeAsync(() => notifier.ProjectionChangedForTenant += Raise.Event<Action<string, string>>("tenants", "tenant.alpha"));
        cut.WaitForAssertion(() => HandoffReadCount(gateway).ShouldBeGreaterThan(reads));
        TenantDetailRequest request = gateway.ReceivedCalls().Where(c => c.GetMethodInfo().Name == nameof(ITenantQueryGateway.GetTenantAsync))
            .Last().GetArguments()[0].ShouldBeOfType<TenantDetailRequest>();
        request.TenantId.ShouldBe("tenant.alpha");
        request.ETag.ShouldBe("\"etag\"");
        await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        callbacks.Count.ShouldBe(0);
        await AssertObsoleteHandoffCallbackAsync(cut, gateway, obsoleteCallback);
        cut.Markup.ShouldNotContain("private late provider failure");
    }

    private void RegisterThrowingNotificationLogger()
    {
        var logger = Substitute.For<Microsoft.Extensions.Logging.ILogger<TenantReadRefreshSubscription>>();
        logger.WhenForAnyArgs(log => log.IsEnabled(default))
            .Do(_ => throw new InvalidOperationException("private transport diagnostics"));
        Services.AddSingleton(logger);
    }

    private void RegisterHandoffServices(ITenantQueryGateway gateway, IProjectionSubscription backend, IProjectionChangeNotifierWithTenant notifier)
    {
        Services.AddSingleton(gateway);
        Services.AddSingleton(backend);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();
    }

    private static T HandoffField<T>(IRenderedComponent<TenantDetailPage> cut, string name)
        => (T)typeof(TenantDetailPage).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(cut.Instance)!;

    private static int HandoffReadCount(ITenantQueryGateway gateway)
        => gateway.ReceivedCalls().Count(call => call.GetMethodInfo().Name is
            nameof(ITenantQueryGateway.GetTenantAsync) or nameof(ITenantQueryGateway.GetTenantUsersAsync));

    private static Task AssertObsoleteHandoffCallbackAsync(
        IRenderedComponent<TenantDetailPage> cut, ITenantQueryGateway gateway, Func<Task> callback)
        => cut.InvokeAsync(async () =>
        {
            int reads = HandoffReadCount(gateway);
            TenantDetailSnapshot detail = HandoffField<TenantDetailSnapshot>(cut, "_snapshot");
            TenantUsersSnapshot members = HandoffField<TenantUsersSnapshot>(cut, "_tenantUsersSnapshot");
            await callback();
            HandoffReadCount(gateway).ShouldBe(reads, "a copied obsolete callback must issue no detail/member reads");
            HandoffField<TenantDetailSnapshot>(cut, "_snapshot").ShouldBeSameAs(detail);
            HandoffField<TenantUsersSnapshot>(cut, "_tenantUsersSnapshot").ShouldBeSameAs(members);
        });

    /// <summary>
    /// Optional notification setup must stop retrying a backend that keeps failing.
    /// </summary>
    /// <remarks>
    /// <c>OnAfterRenderAsync</c> retries whenever no lease was recorded, and a faulting subscribe records
    /// neither <c>_readRefreshLease</c> nor <c>_subscriptionTenantId</c>, so every guard passed again on the
    /// next render -- two remote round trips per render, unbounded, for the life of the circuit. A new route
    /// still gets its own attempts.
    /// </remarks>
    [Fact]
    public void Read_refresh_setup_retries_are_bounded_per_route()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backendSubscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        backendSubscription
            .SubscribeAsync("tenants", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("subscription endpoint is down")));
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ReadyWithSafeConfiguration(
                Detail(call.Arg<TenantDetailRequest>()!.TenantId),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>()!.TenantId))));
        Services.AddSingleton(gateway);
        Services.AddSingleton(backendSubscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");

        for (int render = 0; render < 12; render++)
        {
            cut.Render();
        }

        int alphaAttempts = backendSubscription.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IProjectionSubscription.SubscribeAsync)
                && (string)call.GetArguments()[1]! == "tenant.alpha");
        // Exact, not a range. `<= 3` paired with `>= 1` cannot tell "bounded at three retries" from "never
        // retried at all", so reducing the budget to zero passed. Twelve renders follow the first failure,
        // so a correct budget spends exactly its three attempts and no more.
        alphaAttempts.ShouldBe(3, "A failing subscribe must not be retried on every render.");

        // A new route gets its own budget: the bound is per tenant, not per circuit. `>= 1` could not show
        // that -- a route change makes one subscribe attempt whatever the budget does -- so this asserts
        // beta genuinely re-spends a budget rather than inheriting alpha's exhausted one.
        cut.Render(parameters => parameters.Add(page => page.TenantId, "tenant.beta"));
        for (int render = 0; render < 12; render++)
        {
            cut.Render();
        }

        cut.WaitForAssertion(() => backendSubscription.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IProjectionSubscription.SubscribeAsync)
                && (string)call.GetArguments()[1]! == "tenant.beta")
            .ShouldBe(3));
    }

    [Fact]
    public async Task Same_route_member_refresh_reopens_the_notification_setup_budget()
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backendSubscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();
        backendSubscription
            .SubscribeAsync("tenants", "tenant.alpha", Arg.Any<CancellationToken>())
            .Returns(_ => Task.FromException(new InvalidOperationException("subscription endpoint is down")));
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(MemberSnapshot(Detail("tenant.alpha"))));
        Services.AddSingleton(gateway);
        Services.AddSingleton(backendSubscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-table']");
        for (int render = 0; render < 12; render++)
        {
            cut.Render();
        }

        CountAttempts().ShouldBe(3);

        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());
        for (int render = 0; render < 12; render++)
        {
            cut.Render();
        }

        cut.WaitForAssertion(() => CountAttempts().ShouldBe(6));

        int CountAttempts()
            => backendSubscription.ReceivedCalls()
                .Count(call => call.GetMethodInfo().Name == nameof(IProjectionSubscription.SubscribeAsync)
                    && (string)call.GetArguments()[1]! == "tenant.alpha");
    }

    /// <summary>
    /// A dispose racing a suspended subscribe continuation must not leave the lease attached.
    /// </summary>
    /// <remarks>
    /// The pre-assignment <c>_disposed</c> check is not enough: <c>DisposeAsync</c> sets <c>_disposed</c> and
    /// then reads a still-null <c>_readRefreshLease</c>, so a continuation that passed that check and was
    /// preempted assigned the lease afterwards — leaving the callback registered for the life of the circuit
    /// and invoking <c>InvokeAsync</c> on a disposed component.
    /// <para>
    /// Scope, stated honestly: this does NOT pin the post-assignment recheck. Gating inside
    /// <c>SubscribeAsync</c> means a dispose issued from a test always precedes the pre-assignment check, and
    /// the window between that check and the assignment is not reachable from outside the component. The
    /// theory drives both orderings that ARE reachable -- dispose before the subscribe completes, and dispose
    /// after the lease is fully attached -- and asserts the lease ends up released in each.
    /// </remarks>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_dispose_racing_the_subscribe_continuation_does_not_leave_the_lease_attached(
        bool disposeBeforeSubscribeCompletes)
    {
        JSInterop.Mode = JSRuntimeMode.Loose;
        TaskCompletionSource subscribeGate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        IProjectionSubscription backendSubscription = Substitute.For<IProjectionSubscription>();
        IProjectionChangeNotifierWithTenant notifier = Substitute.For<IProjectionChangeNotifierWithTenant>();

        // Suspend inside SubscribeAsync so the dispose lands between the pre-assignment check and the
        // assignment itself — the exact window the recheck exists for.
        backendSubscription
            .SubscribeAsync("tenants", Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(_ => subscribeGate.Task);
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(ReadyWithSafeConfiguration(
                Detail(call.Arg<TenantDetailRequest>()!.TenantId),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call => Task.FromResult(MemberSnapshot(Detail(call.Arg<TenantUsersRequest>()!.TenantId))));
        Services.AddSingleton(gateway);
        Services.AddSingleton(backendSubscription);
        Services.AddSingleton(notifier);
        Services.AddScoped<TenantReadRefreshSubscription>();
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-detail-identity']");
        cut.WaitForAssertion(() => backendSubscription.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IProjectionSubscription.SubscribeAsync))
            .ShouldBeGreaterThanOrEqualTo(1));

        // Both interleavings, run explicitly rather than hoped for. Gating inside SubscribeAsync means the
        // continuation cannot resume until the gate is released, so disposing first ALWAYS lands before the
        // pre-assignment check and the post-assignment recheck is never reached: the single-ordering version
        // of this test claimed to pin that recheck and could not, because nothing forces a dispose into the
        // window between the check and the assignment from outside the component. What is actually
        // guaranteed -- and is what matters to a user -- is that the lease ends up released under either
        // ordering, so both are now driven.
        if (disposeBeforeSubscribeCompletes)
        {
            // Dispose reaches the pre-assignment check first; the lease is never attached.
            await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
            subscribeGate.SetResult();
        }
        else
        {
            // The lease is fully attached first, so disposal must detach it through the ordinary path.
            subscribeGate.SetResult();
            cut.WaitForAssertion(() => backendSubscription.ReceivedCalls()
                .Count(call => call.GetMethodInfo().Name == nameof(IProjectionSubscription.SubscribeAsync))
                .ShouldBeGreaterThanOrEqualTo(1));
            await cut.InvokeAsync(async () => await cut.Instance.DisposeAsync());
        }

        // Disposal runs through the renderer's dispatcher, as it does in production. Calling DisposeAsync
        // straight from the test thread bypassed the dispatcher entirely, so the path under test was not
        // running under the conditions it runs under for real.
        cut.WaitForAssertion(() => backendSubscription.ReceivedCalls()
            .Count(call => call.GetMethodInfo().Name == nameof(IProjectionSubscription.UnsubscribeAsync)
                && (string)call.GetArguments()[1]! == "tenant.alpha")
            .ShouldBe(1));
    }

    /// <summary>
    /// The qualified member absence copy must be one whole localized sentence per state.
    /// </summary>
    /// <remarks>
    /// The message was assembled at run time as
    /// <c>$"{Localizer["Tenants.Members.Empty.Message", …]} {StateMessage}"</c>, gluing two independently
    /// translated sentences with an ASCII space. Project rules require whole strings with placeholders: a
    /// translator cannot reorder, re-punctuate, or make the two halves agree, and no assertion could
    /// distinguish the composed form from a genuine resource.
    /// </remarks>
    [Theory]
    [InlineData(ReadModelFreshnessState.Stale, "Tenants.Members.Empty.Message.Stale")]
    [InlineData(ReadModelFreshnessState.Unknown, "Tenants.Members.Empty.Message.Unknown")]
    public void Qualified_member_absence_uses_one_whole_localized_string(
        ReadModelFreshnessState freshness,
        string expectedKey)
    {
        RegisterComponentServices();
        TenantDetail detail = Detail(
            "tenant.alpha",
            new Dictionary<string, string>(),
            TenantStatus.Active,
            []);
        TenantUsersSnapshot members = TenantUsersSnapshot.Empty(
            detail.TenantId,
            isAuthorizationScoped: true,
            eTag: "members-etag",
            projectionVersion: "tenant-sequence:1",
            freshness,
            ProjectionLifecycleState.Unknown) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };

        var resources = new ResourceManager(
            "Hexalith.Tenants.UI.Resources.TenantsResources",
            typeof(TenantsResources).Assembly);
        string english = resources.GetString(expectedKey, CultureInfo.InvariantCulture)
            .ShouldNotBeNull();
        string french = resources.GetString(expectedKey, new CultureInfo("fr"))
            .ShouldNotBeNull();

        // Whole strings on both sides of the parity contract, with the one placeholder the reason label fills.
        english.ShouldContain("{0}");
        french.ShouldContain("{0}");
        french.ShouldNotBe(english);

        // Rendered under each culture in turn. Looking the resource up here and asserting the *English*
        // rendering contains it proved nothing about localization: a component that had hard-coded the
        // English literal produced byte-identical output and passed, and `french.ShouldNotBe(english)` only
        // showed the .fr.resx differs -- never that production reads it. Asserting the French rendering
        // carries French copy, and not the English copy, is what makes the localizer load-bearing.
        string renderedEnglish = RenderMemberEmptyText(CultureInfo.InvariantCulture);
        string renderedFrench = RenderMemberEmptyText(new CultureInfo("fr"));

        AssertCarriesWholeString(renderedEnglish, english);
        AssertCarriesWholeString(renderedFrench, french);
        renderedFrench.ShouldNotBe(renderedEnglish);
        renderedEnglish.ShouldNotContain(FirstSegment(french));

        string RenderMemberEmptyText(CultureInfo culture)
        {
            CultureInfo previousUi = CultureInfo.CurrentUICulture;
            CultureInfo previous = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentUICulture = culture;
                CultureInfo.CurrentCulture = culture;
                IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
                    .Add(p => p.AuditProofCapabilityAvailable, true)
                    .Add(view => view.Detail, detail)
                    .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
                    .Add(view => view.Freshness, ReadModelFreshnessState.Current)
                    .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
                    .Add(view => view.ProjectionVersion, "tenant-sequence:1")
                    .Add(view => view.Members, members));
                return cut.Find("[data-testid='tenants-member-empty']").TextContent;
            }
            finally
            {
                CultureInfo.CurrentUICulture = previousUi;
                CultureInfo.CurrentCulture = previous;
            }
        }

        // Both segments must be non-empty, or the assertion silently half-disarms: `{0}` at the very start
        // or very end yields an empty slice, and ShouldContain("") always passes.
        static string FirstSegment(string resource)
            => resource[..resource.IndexOf("{0}", StringComparison.Ordinal)].Trim();

        static void AssertCarriesWholeString(string rendered, string resource)
        {
            string head = FirstSegment(resource);
            string tail = resource[(resource.IndexOf("{0}", StringComparison.Ordinal) + 3)..].Trim();
            head.ShouldNotBeNullOrWhiteSpace();
            tail.ShouldNotBeNullOrWhiteSpace();
            rendered.ShouldContain(head);
            rendered.ShouldContain(tail);
        }
    }

    /// <summary>
    /// A degraded member read over a previously authorization-scoped-empty page must not read as "No members".
    /// </summary>
    /// <remarks>
    /// <c>TenantUsersSnapshot.Degraded</c> retained the previous snapshot's <c>IsAuthorizationScopedEmpty</c>,
    /// and <c>IsAuthorizationSafeAbsence</c> short-circuits the whole state switch, so a read the gateway
    /// could not complete presented as a successful authorized absence -- the exact conflation AC6 forbids.
    /// </remarks>
    [Fact]
    public void Degraded_member_read_over_an_authorized_empty_page_is_not_an_authorization_safe_absence()
    {
        RegisterComponentServices();
        TenantDetail detail = Detail(
            "tenant.alpha",
            new Dictionary<string, string>(),
            TenantStatus.Active,
            []);
        TenantUsersSnapshot authorizedEmpty = TenantUsersSnapshot.Empty(
            detail.TenantId,
            isAuthorizationScoped: true,
            eTag: "members-etag",
            projectionVersion: "tenant-sequence:1",
            ReadModelFreshnessState.Current,
            ProjectionLifecycleState.Current) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };
        authorizedEmpty.IsAuthorizationScopedEmpty.ShouldBeTrue();

        TenantUsersSnapshot degraded = TenantUsersSnapshot.Degraded(
            detail.TenantId,
            authorizedEmpty,
            TenantUsersReason.GatewayUnavailable) with { CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha") };
        degraded.IsAuthorizationScopedEmpty.ShouldBeFalse();

        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, degraded));

        cut.Find("[data-testid='tenants-member-degraded']");
        cut.FindAll("[data-testid='tenants-member-empty']").ShouldBeEmpty();
    }

    /// <summary>
    /// A denial member refresh must release a captured command flow.
    /// </summary>
    /// <remarks>
    /// The flows fall back to their captured member once the row leaves the page, which is correct for a
    /// removal confirmation. On an Unauthorized, NotFound or Invalid refresh the replacement snapshot keeps
    /// <c>RequestCursor</c> null, so the cursor-change reset in <c>OnParametersSet</c> never fires: rows
    /// blanked to the denial state while the flow region kept rendering the captured member's identifier and
    /// role, and both pager buttons stayed disabled with no flow the operator could close.
    /// <para>
    /// The rows are what makes this correct rather than merely tidy: after a denial the caller may no longer
    /// see this membership at all, so continuing to render the captured member's identifier and role would
    /// leak what authorization-safe absence forbids. Review loop 12 cut Unavailable and Error out of this
    /// theory — see the outage sibling below for why they must do the opposite — and added Invalid, which
    /// production listed and no row exercised.
    /// </para>
    /// </remarks>
    [Theory]
    [InlineData(TenantUsersSurfaceKind.Unauthorized, "tenants-member-unauthorized")]
    [InlineData(TenantUsersSurfaceKind.NotFound, "tenants-member-not-found")]
    [InlineData(TenantUsersSurfaceKind.Invalid, "tenants-member-invalid")]
    public void Terminal_member_refresh_releases_a_captured_command_flow(
        TenantUsersSurfaceKind kind,
        string expectedTestId)
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));
        cut.FindAll("[data-testid='tenants-remove-member-open']")[0].Click();
        cut.Find("[data-testid='tenants-remove-member-flow']");

        cut.Render(parameters => parameters
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.CanGoPrevious, true)
            .Add(view => view.Members, TerminalMemberSnapshot(kind, detail.TenantId)));

        cut.Find($"[data-testid='{expectedTestId}']");
        cut.FindAll("[data-testid='tenants-remove-member-flow']").ShouldBeEmpty();

        // ...and the pager is released with it, rather than staying disabled behind a flow that is gone.
        cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeFalse();
    }

    /// <summary>
    /// An operational member-read failure must NOT tear down a captured command flow.
    /// </summary>
    /// <remarks>
    /// Unavailable and Error are outages, not denials: the caller's rights are unchanged and a command that
    /// was really submitted still owns a receipt to render. Folding them in with the denial kinds let one
    /// Tenants API blip destroy an in-flight member removal's Confirmed / Rejected / UnableToVerify state,
    /// its safe message and its audit entry point — and because no member flow implements disposal, the
    /// command-activity lease was never lowered either, so every command surface on the detail page stayed
    /// disabled for the life of the circuit.
    /// </remarks>
    [Theory]
    [InlineData(TenantUsersSurfaceKind.Unavailable)]
    [InlineData(TenantUsersSurfaceKind.Error)]
    public void An_operational_member_read_failure_keeps_a_captured_command_flow(TenantUsersSurfaceKind kind)
    {
        RegisterComponentServices();
        TenantDetail detail = Detail("tenant.alpha");
        IRenderedComponent<MemberAccessReview> cut = Render<MemberAccessReview>(parameters => parameters
            .Add(p => p.IsAddMemberAuthorized, true)
            .Add(p => p.IsChangeRoleAuthorized, true)
            .Add(p => p.IsRemoveMemberAuthorized, true)
            .Add(p => p.AuthorizationReflection, TenantLifecycleAuthorizationReflectionState.Authorized)
            .Add(p => p.AuthorizationReflectionProvider, _ => Task.FromResult(TenantLifecycleAuthorizationReflectionState.Authorized))
            .Add(p => p.AuditProofCapabilityAvailable, true)
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.Members, MemberSnapshot(detail)));
        cut.FindAll("[data-testid='tenants-remove-member-open']")[0].Click();
        cut.Find("[data-testid='tenants-remove-member-flow']");

        cut.Render(parameters => parameters
            .Add(view => view.Detail, detail)
            .Add(view => view.SurfaceKind, TenantDetailSurfaceKind.Ready)
            .Add(view => view.Freshness, ReadModelFreshnessState.Current)
            .Add(view => view.Lifecycle, ProjectionLifecycleState.Current)
            .Add(view => view.ProjectionVersion, "tenant-sequence:1")
            .Add(view => view.CanGoPrevious, true)
            .Add(view => view.Members, TerminalMemberSnapshot(kind, detail.TenantId)));

        cut.Find("[data-testid='tenants-remove-member-flow']");
    }

    /// <summary>
    /// The member pager announces a Previous step that lands on page one through a trimmed history.
    /// </summary>
    /// <remarks>
    /// <c>CursorHistory.Trim</c> re-appends the first-page sentinel beneath the newest entries, so one later
    /// Previous click walks the operator from mid-sequence straight to page one. The audit and
    /// global-administrator pagers announce that; this one discarded <c>Trim</c>'s return value entirely and
    /// rendered the jump as an ordinary step back.
    /// </remarks>
    [Fact]
    public async Task The_member_pager_announces_a_previous_step_that_jumps_to_the_first_page()
    {
        // Driven through TenantDetailPage's real pager, which is where the defect lived: the notice is
        // computed by the PARENT from `_memberPagingHistoryTruncated && _memberCursor is null &&
        // _memberCursorHistory.Count == 0`, and the remark names the defect as discarding `CursorHistory.Trim`'s
        // return value. Setting MemberAccessReview.PagingJumpedToFirstPage as a child parameter asserted only
        // that the child renders a notice when told to; the parent's computation had no test at all, so
        // hard-coding it to false, or deleting the Trim call, left the whole suite green. Both sibling pagers
        // drive the real control, and this now matches them.
        const int bound = CursorHistory.DefaultMaximum;
        JSInterop.Mode = JSRuntimeMode.Loose;
        ITenantQueryGateway gateway = Substitute.For<ITenantQueryGateway>();
        gateway.GetTenantAsync(Arg.Any<TenantDetailRequest>(), Arg.Any<TenantDetailSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(ReadyWithSafeConfiguration(
                Detail("tenant.alpha"),
                ProjectionLifecycleState.Current,
                "projection-v1")));
        gateway.GetTenantUsersAsync(Arg.Any<TenantUsersRequest>(), Arg.Any<TenantUsersSnapshot?>(), Arg.Any<CancellationToken>())
            .Returns(call =>
            {
                TenantUsersRequest request = call.Arg<TenantUsersRequest>()!;
                int page = request.Cursor is null
                    ? 0
                    : int.Parse(request.Cursor["page-".Length..], CultureInfo.InvariantCulture);
                return Task.FromResult(TenantUsersSnapshot.Ready(
                    "tenant.alpha",
                    [new TenantMember($"user-page-{page}", TenantRole.TenantOwner)],
                    $"page-{page + 1}",
                    true,
                    $"members-{page}",
                    "projection-v1",
                    ReadModelFreshnessState.Current,
                    ProjectionLifecycleState.Current) with
                {
                    CallerScope = TenantQueryGateway.AuditCallerScope("operator.alpha"),
                    RequestCursor = request.Cursor,
                });
            });
        Services.AddSingleton(gateway);
        Services.AddSingleton<IStringLocalizer<TenantsResources>>(new StubTenantsLocalizer());
        Services.AddSingleton<ITenantCommandGateway>(new StubTenantCommandGateway());
        Services.AddSingleton<ITenantsBffComposition>(new StubTenantsBffComposition());
        Services.AddFluentUIComponents();

        IRenderedComponent<TenantDetailPage> cut = Render<TenantDetailPage>(parameters => parameters
            .Add(page => page.TenantId, "tenant.alpha"));
        cut.WaitForElement("[data-testid='tenants-member-next']");

        // One page past the bound, so the trim runs and drops the oldest non-sentinel entries.
        for (int page = 1; page <= bound + 1; page++)
        {
            cut.Find("[data-testid='tenants-member-next']").Click();
            cut.WaitForAssertion(() => cut.Markup.ShouldContain($"user-page-{page}"));
        }

        cut.FindAll("[data-testid='tenants-member-history-truncated']").ShouldBeEmpty(
            "Paging forward is not a jump; the notice belongs to the Previous click that lands on page one.");

        // Walk back. The retained history is the bound minus one entries plus the re-appended sentinel, so
        // the last of these pops the sentinel and lands on page one from the middle of the sequence.
        for (int step = 1; step < bound; step++)
        {
            cut.Find("[data-testid='tenants-member-previous']").Click();
            cut.WaitForAssertion(() => cut.Find("[data-testid='tenants-member-previous']")
                .HasAttribute("disabled").ShouldBeFalse());
            cut.FindAll("[data-testid='tenants-member-history-truncated']").ShouldBeEmpty();
        }

        cut.Find("[data-testid='tenants-member-previous']").Click();
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-member-history-truncated']").ShouldNotBeEmpty());

        IElement notice = cut.Find("[data-testid='tenants-member-history-truncated']");
        notice.GetAttribute("role").ShouldBe("status");
        notice.GetAttribute("aria-live").ShouldBe("polite");

        // The sibling pagers assert the copy, not merely that some text is present: swapping the resource key
        // for an unrelated recovery string passed the previous `ShouldNotBeNullOrWhiteSpace` check.
        notice.TextContent.ShouldContain("first page", Case.Insensitive);
        cut.Find("[data-testid='tenants-member-previous']").HasAttribute("disabled").ShouldBeTrue();

        await cut.InvokeAsync(() => cut.FindComponent<MemberAccessReview>().Instance
            .OnProjectionRefreshRequested.InvokeAsync());
        cut.WaitForAssertion(() => cut.FindAll("[data-testid='tenants-member-history-truncated']").ShouldBeEmpty(
            "the page-one jump notice is one-shot evidence and a later authoritative refresh clears it"));
    }
}
